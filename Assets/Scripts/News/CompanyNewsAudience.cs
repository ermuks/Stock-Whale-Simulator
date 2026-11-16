using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Budgeted reading, not a global sentiment broadcast. Uses existing social posting and actual accounts.</summary>
public sealed class CompanyNewsAudience : MonoBehaviour
{
    public static CompanyNewsAudience Instance { get; private set; }
    [Range(1, 48)] public int botsPerTick = 24;
    [Min(.1f)] public float interval = .25f;
    [Min(20)] public float readingWindow = 90;
    [Range(0, 1)] public float witnessChance = .45f;
    [Range(0, 1)] public float speechChance = .12f;
    private sealed class Notice
    {
        public NewsArticle article;
        public Stock stock;
        public readonly HashSet<int> attempted = new();
        public int speeches;
    }
    private readonly List<Notice> notices = new(16);
    private sealed class Response { public string name; public SocialMessage message; }
    private readonly Dictionary<long, List<Response>> publicResponses = new();
    private readonly Dictionary<int, float> nextRead = new();
    private float next;
    private int cursor;
    private InvestorSociety boundSociety;
    public int Version { get; private set; }
    public int Observations { get; private set; }
    void Awake() { Instance = this; }
    void OnEnable() { NewsManager.OnArticlePublished += Published; }
    void OnDisable()
    {
        NewsManager.OnArticlePublished -= Published;
        if (boundSociety != null) { boundSociety.SocialMessagePublished -= Spoken; }
        boundSociety = null;
    }
    void OnDestroy() { if (Instance == this) { Instance = null; } }
    private static Stock Find(string symbol)
    {
        var manager = StockManager.Instance;
        if (manager == null) { return null; }
        foreach (var stock in manager.stocks) { if (stock.symbol == symbol && !stock.IsDelisted) { return stock; } }
        return null;
    }
    private void Published(NewsArticle article)
    {
        var stock = Find(article.symbol); if (stock == null) { return; }
        if (notices.Count == 16) { notices.RemoveAt(0); }
        notices.Add(new Notice { article = article, stock = stock });
        if (publicResponses.Count >= 40)
        {
            long oldest = long.MaxValue; foreach (long key in publicResponses.Keys) { oldest = Math.Min(oldest, key); }
            publicResponses.Remove(oldest);
        }
        publicResponses[article.id] = new List<Response>(3);
    }
    void Update()
    {
        if (SessionClock.Time < next || MarketDayController.Instance == null || InvestorSociety.Instance == null) { return; }
        next = SessionClock.Time + interval;
        var society = InvestorSociety.Instance;
        if (boundSociety != society)
        {
            if (boundSociety != null) { boundSociety.SocialMessagePublished -= Spoken; }
            boundSociety = society; boundSociety.SocialMessagePublished += Spoken;
        }
        notices.RemoveAll(item => item.stock.IsDelisted || SessionClock.Time - item.article.publishedAt > readingWindow);
        var bots = MarketDayController.Instance.Bots;
        for (int i = 0; i < Math.Min(botsPerTick, bots.Count); i++)
        {
            if (cursor >= bots.Count) { cursor = 0; }
            var bot = bots[cursor++];
            if (bot?.Social == null || bot.IsExchangeParticipant || !society.Present(bot, 0)) { continue; }
            var mind = bot.Social.Psychology;
            if (!mind.ActiveAt(SessionClock.Time) || nextRead.TryGetValue(bot.Social.Id, out float until) && SessionClock.Time < until) { continue; }
            // Oldest first: a later correction cannot be read before the original and then overwritten by it.
            foreach (var notice in notices)
            {
                if (!notice.attempted.Add(bot.Social.Id)) { continue; }
                var article = notice.article; var opinion = mind.Companies.Read(article.symbol);
                bool held = bot.GetOwnedVolume(notice.stock) > 0;
                bool interested = held || mind.WatchedStock == notice.stock || opinion.affinity >= 60;
                double roll = NewsWriting.Hash(bot.Social.Id + ":" + article.id + ":read") / (double)uint.MaxValue;
                if (roll >= witnessChance * (.5 + mind.Activity) * (interested ? 1 : .2)) { continue; }
                if (!mind.Companies.Observe(article.symbol, article.id, article.sentiment, article.credibility, article.correction, mind.Credulity, held, SessionClock.Time)) { continue; }
                nextRead[bot.Social.Id] = SessionClock.Time + 8; Observations++;
                ChatSocialAudit.Journal.Event(new SocialEvent { id = article.id, time = ChatSocialAudit.GameMinute, actor = bot.Social.Id,
                    kind = "COMPANY_NEWS_READ", reason = article.symbol + " " + mind.Companies.Stance(article.symbol, article.sentiment, article.correction), value = mind.Companies.TradeScale(article.symbol, true, SessionClock.Time) });
                int linked = 0;
                foreach (var link in article.links)
                {
                    var related = Find(link.symbol);
                    if (related == null || bot.GetOwnedVolume(related) <= 0 && mind.WatchedStock != related) { continue; }
                    mind.Companies.Observe(link.symbol, article.id, article.sentiment * link.transmission, article.credibility * .6, false, mind.Credulity, bot.GetOwnedVolume(related) > 0, SessionClock.Time);
                    if (++linked >= 2) { break; }
                }
                double speaking = NewsWriting.Hash(bot.Social.Id + ":" + article.id + ":speak") / (double)uint.MaxValue;
                if (notice.speeches < 2 && speaking < speechChance * (.5 + mind.Traits.Sociability / 100d))
                { notice.speeches++; society.OfferCompanyNews(bot, notice.stock, article, article.sentiment); }
                break;
            }
        }
    }
    private void Spoken(SocialMessage message)
    {
        if (message.roomId != 0 || message.facts == null || !publicResponses.TryGetValue(message.facts.companyNewsId, out var responses)) { return; }
        string name = MarketDayController.Instance?.FindBot(message.senderId)?.Social.Nickname ?? "참여자";
        if (responses.Count == 3) { responses.RemoveAt(0); }
        GameLocale.ProtectLiteral(name);
        responses.Add(new Response { name = name, message = message }); Version++;
    }
    public string Responses(long id)
    {
        if (!publicResponses.TryGetValue(id,out var rows) || rows.Count == 0) { return GameLocale.Text("이 기사에 직접 연결된 공개 발언은 아직 없습니다."); }
        var result = new System.Text.StringBuilder();
        foreach (var row in rows)
        {
            if (result.Length > 0) { result.Append("\n\n"); }
            result.Append(row.name).Append("  ").Append(LocalizedChat.Text(row.message,row.message.text));
        }
        return result.ToString();
    }
}
