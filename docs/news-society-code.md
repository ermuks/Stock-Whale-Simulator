# 뉴스·기업 심리·태블릿 조작부 코드 기록

2026-11-15. 이 작업 직전에 저장한 원본과 비교한다. 기존 미커밋 변경을 HEAD로 되돌리지 않았다. 신규 C# 전문과 기존 함수의 원본/수정본을 함께 제공한다. 임시 촬영기는 검증 폴더에 보관하며 게임 빌드에서 제외한다.

## 신규 C# 전문

### Assets/Scripts/News/NewsArticle.cs

```csharp
using System;
using System.Collections.Generic;
using System.Text;

[Serializable]
public sealed class NewsArticle
{
    public long id, storyId;
    public string symbol, company, title, summary, body, category, categoryLabel, source, verification, time;
    public double publishedAt, sentiment, credibility;
    public bool fabricated, correction;
    public NewsCompanyLink[] links = Array.Empty<NewsCompanyLink>();
}

[Serializable] public sealed class NewsCompanyLink
{
    public string symbol, relation;
    public double transmission;
}
[Serializable] public sealed class NewsCompanyProfile
{
    public string symbol, sector, community;
    public NewsCompanyLink[] links;
}
[Serializable] public sealed class NewsNarrative
{
    public string key, label;
    public string[] keywords, context, stakeholders, questions, positiveRumors, negativeRumors;
}
[Serializable] public sealed class NewsWritingCatalog
{
    public NewsNarrative[] narratives;
    public NewsCompanyProfile[] companies;
    public NewsNarrative Find(string key)
    {
        foreach (var item in narratives) { if (item.key == key) { return item; } }
        return narratives[narratives.Length - 1];
    }
    public NewsCompanyProfile Profile(string symbol)
    {
        foreach (var item in companies) { if (item.symbol == symbol) { return item; } }
        return new NewsCompanyProfile { symbol = symbol, sector = "기업", community = "이용자와 지역사회", links = Array.Empty<NewsCompanyLink>() };
    }
}

/// <summary>Fictional in-world reporting. Variation never consumes market or social RNG.</summary>
public static class NewsWriting
{
    public static uint Hash(string value)
    {
        uint hash = 2166136261;
        foreach (char letter in value ?? "") { hash = unchecked((hash ^ letter) * 16777619); }
        hash ^= hash >> 16; hash = unchecked(hash * 0x7feb352d); return hash ^ (hash >> 15);
    }
    public static NewsArticle Compose(NewsWritingCatalog catalog, long id, string symbol, string company, string title, double sentiment,
        string time, double at, bool fabricated = false, string category = null, int variant = 0, long storyId = 0, bool correction = false)
    {
        var narrative = catalog.Find(category ?? Classify(catalog, title));
        var profile = catalog.Profile(symbol);
        uint key = Hash(symbol + ":" + (fabricated ? variant : id) + ":" + title + ":" + variant);
        string Pick(string[] values, uint salt) => values[(int)(Hash(key + ":" + salt) % (uint)values.Length)];
        string Expand(string value) => value.Replace("{company}", company).Replace("{sector}", profile.sector).Replace("{community}", profile.community);
        string summary = correction ? "앞서 유통된 주장에 대한 후속 확인입니다. 최초 보도와 확인 결과를 함께 읽어야 합니다." :
            fabricated ? company + " 관련 주장이 익명 채널에서 유통되고 있습니다. 확인된 계약이나 실적 자료는 아직 제시되지 않았습니다." :
            company + "의 새 소식이 전해졌습니다. 기업 가치뿐 아니라 고객·협력사·투자자가 받아들이는 맥락도 살펴볼 필요가 있습니다.";
        var text = new StringBuilder();
        text.Append(correction ? "후속 확인" : fabricated ? "전해진 주장" : "무슨 일이 있었나").Append('\n')
            .Append(company).Append(" 관련 소식의 핵심은 ‘").Append(title).Append("’입니다. ")
            .Append(correction ? "앞선 주장과 다른 확인 결과가 나왔습니다. 원문을 믿고 판단했던 투자자는 근거를 다시 살펴볼 시점입니다." :
                fabricated ? "출처가 명확한 증빙이나 회사의 공식 확인은 없습니다. 온라인의 반복 인용을 별도의 확인으로 보기는 어렵습니다." :
                "이번 소식만으로 매출 반영 시점이나 최종 비용까지 확정된 것은 아닙니다.")
            .Append("\n\n배경과 이해관계\n").Append(Expand(Pick(narrative.context, 1)))
            .Append("\n\n사회가 받아들이는 방식\n").Append(Expand(Pick(narrative.stakeholders, 2)))
            .Append("\n\n다음 확인 지점\n").Append(Expand(Pick(narrative.questions, 3)));
        return new NewsArticle { id = id, storyId = storyId == 0 ? id : storyId, symbol = symbol, company = company,
            title = title, summary = summary, body = text.ToString(), category = narrative.key, categoryLabel = narrative.label,
            source = correction ? "돈돈 팩트체크" : fabricated ? "마켓와이어 · 익명 제보" : "돈돈 경제 · 기업 브리핑",
            verification = correction ? "후속 확인" : fabricated ? "미확인 보도" : "기업 동향", time = time, publishedAt = at,
            sentiment = Math.Max(-1, Math.Min(1, sentiment)), credibility = correction ? .95 : fabricated ? .32 : .8,
            fabricated = fabricated, correction = correction, links = profile.links ?? Array.Empty<NewsCompanyLink>() };
    }
    public static string Classify(NewsWritingCatalog catalog, string title)
    {
        string category = "company"; int first = int.MaxValue;
        foreach (var item in catalog.narratives)
        {
            foreach (string keyword in item.keywords)
            {
                if (string.IsNullOrEmpty(keyword)) { continue; }
                int position = (title ?? "").IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (position >= 0 && position < first) { first = position; category = item.key; }
            }
        }
        return category;
    }
    public static string RumorTitle(NewsWritingCatalog catalog, string symbol, string company, string category, bool positive, int variant)
    {
        var narrative = catalog.Find(category);
        var choices = positive ? narrative.positiveRumors : narrative.negativeRumors;
        return choices[(int)(Hash(symbol + ":" + variant) % (uint)choices.Length)].Replace("{company}", company);
    }
    public static bool Matches(NewsArticle article, string query, string category, string selectedSymbol, bool ownedOnly, bool owned, bool unconfirmedOnly)
    {
        if (article == null || category != "all" && article.category != category || !string.IsNullOrEmpty(selectedSymbol) && article.symbol != selectedSymbol ||
            ownedOnly && !owned || unconfirmedOnly && (article.correction || article.credibility >= .5))
        { return false; }
        return string.IsNullOrWhiteSpace(query) || (article.title + " " + article.company + " " + article.symbol + " " + article.summary)
            .IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

/// <summary>Monotonic real seconds while playing; a snapshot can account for elapsed offline wall time.</summary>
public sealed class NewsPublicationClock
{
    public const double CooldownSeconds = 600;
    private double deadline;
    public double Remaining(double realSeconds) => Math.Max(0, deadline - realSeconds);
    public bool TryConsume(double realSeconds)
    {
        if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0 || Remaining(realSeconds) > 0) { return false; }
        deadline = realSeconds + CooldownSeconds; return true;
    }
    public bool Restore(double remaining, double elapsedOffline, double realSeconds)
    {
        if (double.IsNaN(remaining) || double.IsInfinity(remaining) || remaining < 0 || remaining > CooldownSeconds ||
            double.IsNaN(elapsedOffline) || double.IsInfinity(elapsedOffline) || elapsedOffline < 0 ||
            double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0)
        { return false; }
        deadline = realSeconds + Math.Max(0, remaining - elapsedOffline); return true;
    }
}
```

### Assets/Scripts/News/CompanyOpinionBook.cs

```csharp
using System;
using System.Collections.Generic;

/// <summary>Company attachment is separate from trust in the player. Only observed evidence changes it.</summary>
public sealed class CompanyOpinionBook
{
    [Serializable] public sealed class Opinion
    {
        public string symbol;
        public double affinity, conviction, skepticism, impulse, at, profitWindow = -1, windowCost, windowProfit, appliedProfit;
        public int disappointments;
        public long lastNews;
        public List<long> seen = new();
        public List<long> rumors = new();
    }
    private readonly Dictionary<string, Opinion> opinions = new();
    private readonly int seed, id;
    public CompanyOpinionBook(int seed, int id) { this.seed = seed; this.id = id; }
    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
    public Opinion Read(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) { return null; }
        if (opinions.TryGetValue(symbol, out var value)) { return value; }
        if (opinions.Count >= 32)
        {
            string oldest = null; double oldestAt = double.MaxValue;
            foreach (var row in opinions) { if (row.Value.at < oldestAt) { oldestAt = row.Value.at; oldest = row.Key; } }
            if (oldest != null) { opinions.Remove(oldest); }
        }
        uint hash = NewsWriting.Hash(seed + ":" + id + ":" + symbol);
        value = new Opinion { symbol = symbol, affinity = hash % 100 < 16 ? 65 + hash % 21 : (int)(hash % 81) - 40,
            conviction = 25 + (hash >> 8) % 50, skepticism = 15 + (hash >> 16) % 45 };
        opinions.Add(symbol, value); return value;
    }
    public bool Observe(string symbol, long newsId, double sentiment, double evidence, bool correction, double credulity, bool held, double now)
    {
        if (!Finite(sentiment) || !Finite(evidence) || !Finite(credulity) || !Finite(now)) { return false; }
        sentiment = Clamp(sentiment, -1, 1); evidence = Clamp(evidence, 0, 1);
        var value = Read(symbol);
        if (value == null || newsId <= 0 || value.seen.Contains(newsId)) { return false; }
        if (value.seen.Count == 32) { value.seen.RemoveAt(0); }
        value.seen.Add(newsId); value.lastNews = newsId;
        double loyalty = Math.Max(0, value.affinity) / 100;
        double belief = Clamp(evidence * (.45 + Clamp(credulity, 0, 1) * .55) + (sentiment > 0 ? loyalty * .2 : -loyalty * .15) - value.skepticism / 400, .05, 1);
        double shield = sentiment < 0 ? loyalty * .8 * (1 - evidence * .5) * (.7 + value.conviction / 100 * .3) : 0;
        double interpretation = sentiment * belief * (sentiment > 0 ? 1 + loyalty * .4 : 1 - shield);
        if (sentiment < 0 && evidence < .5 && value.affinity >= 65 && !correction)
        { interpretation = .06 * loyalty; }
        if (sentiment < 0 && evidence >= .65)
        { value.disappointments++; }
        double affectionChange = sentiment * belief * (sentiment < 0 ? 5 + Math.Min(8, value.disappointments) * 2 : 4);
        if (sentiment < 0 && evidence < .5 && loyalty > .65) { affectionChange *= .2; }
        value.affinity = Clamp(value.affinity + affectionChange, -100, 100);
        value.conviction = Clamp(value.conviction + (Math.Sign(sentiment) == Math.Sign(value.affinity) ? 2 : -2) * belief, 0, 100);
        if (correction) { value.skepticism = Clamp(value.skepticism + 8, 0, 100); }
        double previous = Impulse(value, now);
        value.impulse = Clamp(previous * .4 + interpretation * (held ? 1.1 : 1), -1, 1); value.at = now;
        return true;
    }
    public bool Hear(string symbol, long newsId, double opinion, double now)
    {
        if (!Finite(opinion) || !Finite(now)) { return false; }
        var value = Read(symbol);
        if (value == null || newsId <= 0 || value.seen.Contains(newsId) || value.rumors.Contains(newsId)) { return false; }
        if (value.rumors.Count == 32) { value.rumors.RemoveAt(0); }
        value.rumors.Add(newsId); value.affinity = Clamp(value.affinity + Clamp(opinion, -1, 1), -100, 100);
        value.impulse = Clamp(Impulse(value, now) + Clamp(opinion, -1, 1) * .08, -1, 1); value.at = now;
        return true;
    }
    public void Realized(string symbol, double profit, double cost, double now)
    {
        if (cost <= 0 || !Finite(profit) || !Finite(cost) || !Finite(now)) { return; }
        var value = Read(symbol); if (value == null) { return; }
        double window = Math.Floor(now / 60);
        if (window != value.profitWindow)
        { value.profitWindow = window; value.windowCost = value.windowProfit = value.appliedProfit = 0; }
        value.windowCost += cost; value.windowProfit += profit;
        double influence = Clamp(value.windowProfit / value.windowCost * 12, -5, 5);
        value.affinity = Clamp(value.affinity + influence - value.appliedProfit, -100, 100); value.appliedProfit = influence;
    }
    private static double Impulse(Opinion value, double now) => value.impulse * Math.Exp(-Math.Max(0, now - value.at) / 90);
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    public double TradeScale(string symbol, bool buy, double now)
    {
        if (string.IsNullOrEmpty(symbol) || !opinions.TryGetValue(symbol, out var value)) { return 1; }
        double bias = Impulse(value, now) * .3 + value.affinity / 100 * .04;
        return Clamp(1 + (buy ? bias : -bias), .65, 1.35);
    }
    public string Stance(string symbol, double sentiment, bool correction)
    {
        var value = Read(symbol);
        if (correction) { return "CORRECTION"; }
        if (value.affinity >= 60) { return sentiment < 0 ? "DEFEND" : "FAN"; }
        if (value.skepticism >= 50 || value.affinity <= -30) { return "DOUBT"; }
        return sentiment >= 0 ? "HOPE" : "CAUTION";
    }
    public Opinion[] Capture()
    {
        var result = new List<Opinion>();
        foreach (var row in opinions.Values)
        {
            result.Add(new Opinion { symbol = row.symbol, affinity = row.affinity, conviction = row.conviction, skepticism = row.skepticism,
                impulse = row.impulse, at = row.at, profitWindow = row.profitWindow, windowCost = row.windowCost, windowProfit = row.windowProfit,
                appliedProfit = row.appliedProfit, disappointments = row.disappointments, lastNews = row.lastNews,
                seen = new List<long>(row.seen), rumors = new List<long>(row.rumors) });
        }
        result.Sort((a, b) => string.CompareOrdinal(a.symbol, b.symbol)); return result.ToArray();
    }
    public static bool Valid(Opinion[] saved)
    {
        if (saved == null) { return true; }
        if (saved.Length > 32) { return false; }
        var symbols = new HashSet<string>();
        foreach (var row in saved)
        {
            if (row == null || string.IsNullOrEmpty(row.symbol) || !symbols.Add(row.symbol) || row.seen == null || row.rumors == null || row.seen.Count > 32 || row.rumors.Count > 32) { return false; }
            foreach (double n in new[] { row.affinity, row.conviction, row.skepticism, row.impulse, row.at, row.profitWindow, row.windowCost, row.windowProfit, row.appliedProfit })
            { if (double.IsNaN(n) || double.IsInfinity(n)) { return false; } }
            if (Math.Abs(row.affinity) > 100 || row.conviction < 0 || row.conviction > 100 || row.skepticism < 0 || row.skepticism > 100 || Math.Abs(row.impulse) > 1 || row.windowCost < 0 || row.disappointments < 0) { return false; }
        }
        return true;
    }
    public bool Restore(Opinion[] saved)
    {
        if (!Valid(saved)) { return false; }
        opinions.Clear();
        if (saved != null)
        {
            foreach (var row in saved)
            {
                opinions.Add(row.symbol, new Opinion { symbol = row.symbol, affinity = row.affinity, conviction = row.conviction,
                    skepticism = row.skepticism, impulse = row.impulse, at = row.at, profitWindow = row.profitWindow,
                    windowCost = row.windowCost, windowProfit = row.windowProfit, appliedProfit = row.appliedProfit,
                    disappointments = row.disappointments, lastNews = row.lastNews,
                    seen = new List<long>(row.seen), rumors = new List<long>(row.rumors) });
            }
        }
        return true;
    }
}
```

### Assets/Scripts/News/CompanyNewsAudience.cs

```csharp
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
    private readonly Dictionary<long, List<string>> publicResponses = new();
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
        publicResponses[article.id] = new List<string>(3);
    }
    void Update()
    {
        if (Time.time < next || MarketDayController.Instance == null || InvestorSociety.Instance == null) { return; }
        next = Time.time + interval;
        var society = InvestorSociety.Instance;
        if (boundSociety != society)
        {
            if (boundSociety != null) { boundSociety.SocialMessagePublished -= Spoken; }
            boundSociety = society; boundSociety.SocialMessagePublished += Spoken;
        }
        notices.RemoveAll(item => item.stock.IsDelisted || Time.time - item.article.publishedAt > readingWindow);
        var bots = MarketDayController.Instance.Bots;
        for (int i = 0; i < Math.Min(botsPerTick, bots.Count); i++)
        {
            if (cursor >= bots.Count) { cursor = 0; }
            var bot = bots[cursor++];
            if (bot?.Social == null || bot.IsExchangeParticipant || !society.Present(bot, 0)) { continue; }
            var mind = bot.Social.Psychology;
            if (!mind.ActiveAt(Time.time) || nextRead.TryGetValue(bot.Social.Id, out float until) && Time.time < until) { continue; }
            // Oldest first: a later correction cannot be read before the original and then overwritten by it.
            foreach (var notice in notices)
            {
                if (!notice.attempted.Add(bot.Social.Id)) { continue; }
                var article = notice.article; var opinion = mind.Companies.Read(article.symbol);
                bool held = bot.GetOwnedVolume(notice.stock) > 0;
                bool interested = held || mind.WatchedStock == notice.stock || opinion.affinity >= 60;
                double roll = NewsWriting.Hash(bot.Social.Id + ":" + article.id + ":read") / (double)uint.MaxValue;
                if (roll >= witnessChance * (.5 + mind.Activity) * (interested ? 1 : .2)) { continue; }
                if (!mind.Companies.Observe(article.symbol, article.id, article.sentiment, article.credibility, article.correction, mind.Credulity, held, Time.time)) { continue; }
                nextRead[bot.Social.Id] = Time.time + 8; Observations++;
                ChatSocialAudit.Journal.Event(new SocialEvent { id = article.id, time = ChatSocialAudit.GameMinute, actor = bot.Social.Id,
                    kind = "COMPANY_NEWS_READ", reason = article.symbol + " " + mind.Companies.Stance(article.symbol, article.sentiment, article.correction), value = mind.Companies.TradeScale(article.symbol, true, Time.time) });
                int linked = 0;
                foreach (var link in article.links)
                {
                    var related = Find(link.symbol);
                    if (related == null || bot.GetOwnedVolume(related) <= 0 && mind.WatchedStock != related) { continue; }
                    mind.Companies.Observe(link.symbol, article.id, article.sentiment * link.transmission, article.credibility * .6, false, mind.Credulity, bot.GetOwnedVolume(related) > 0, Time.time);
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
        responses.Add(name + "  " + message.text); Version++;
    }
    public string Responses(long id) => publicResponses.TryGetValue(id, out var rows) && rows.Count > 0 ? string.Join("\n\n", rows) : "이 기사에 직접 연결된 공개 발언은 아직 없습니다.";
}
```

### Assets/Scripts/News/NewsDeskUI.cs

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A pooled article reader inside each existing tablet Canvas. UI never modifies market accounts.</summary>
public sealed class NewsDeskUI : MonoBehaviour
{
    private TabletAccountUI navigation;
    private DeviceTerminalUI terminal;
    private TMP_FontAsset font;
    private RectTransform listContent, articleContent;
    private ScrollRect listScroll, articleScroll;
    private TextMeshProUGUI articleText, countText, empty, resultText, draftText, cooldownText;
    private Button all, owned, current, rumors, categoryButton, order, publish, studioButton, draftCategory, draftStock, positive, negative;
    private TMP_InputField search;
    private GameObject reading, studio, picker;
    private readonly List<NewsArticle> visible = new(40);
    private readonly List<Button> rows = new(40);
    private readonly List<TextMeshProUGUI> titles = new(40), meta = new(40);
    private readonly Dictionary<DeviceMarketJournal.Entry, NewsArticle> alerts = new();
    private readonly StringBuilder buffer = new();
    private NewsArticle selected, draft;
    private Stock lastStock, previewStock;
    private string category = "all", query = "", mode = "all";
    private int categoryIndex = -1, draftCategoryIndex = 2, variant, version = -1, responseVersion = -1, alertVersion = -1;
    private bool optimistic = true;
    private float next, resultUntil;
    private static readonly Color Ink = new Color32(230,235,239,255), Muted = new Color32(153,168,179,255), Teal = new Color32(37,175,192,255),
        Surface = new Color32(16,24,29,255), Selected = new Color32(23,53,60,255), Line = new Color32(49,64,72,255);
    public NewsArticle SelectedArticle => selected;
    public int VisibleCount => visible.Count;
    public static NewsDeskUI Install(TabletAccountUI owner)
    {
        if (owner.events == null) { return null; }
        var previous = owner.events.panel.GetComponentInChildren<NewsDeskUI>(true);
        if (previous != null) { return previous; }
        foreach (Transform child in owner.events.panel.transform) { child.gameObject.SetActive(false); }
        var root = new GameObject("Area :: News Reader", typeof(RectTransform));
        root.transform.SetParent(owner.events.panel.transform, false);
        Rect((RectTransform)root.transform, 0, 0, 1, 1);
        var reader = root.AddComponent<NewsDeskUI>(); reader.navigation = owner; reader.terminal = owner.events;
        reader.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading") ?? owner.events.eventDetail.font;
        reader.Build(); return reader;
    }
    private void Build()
    {
        Panel(transform, "News Background", 0, 0, 1, 1, Surface);
        Label(transform, "News Heading", "시장 뉴스", .025f,.018f,.4f,.065f,44,Ink,true);
        search = Input(transform,.60f,.02f,.375f,.055f);
        search.onValueChanged.AddListener(value => { query = value; RefreshList(); });
        all = Button(transform,"All News","전체",.025f,.098f,.12f,.05f,() => SetMode("all"));
        owned = Button(transform,"Owned News","보유 종목",.155f,.098f,.16f,.05f,() => SetMode("owned"));
        current = Button(transform,"Current News","선택 종목",.325f,.098f,.16f,.05f,() => SetMode("current"));
        rumors = Button(transform,"Unconfirmed News","미확인",.495f,.098f,.13f,.05f,() => SetMode("rumors"));
        categoryButton = Button(transform,"News Category","분야 · 전체 ▼",.655f,.098f,.32f,.05f,ChooseCategory);
        reading = Area(transform,"News Reading",0,.17f,1,.75f).gameObject;
        countText = Label(reading.transform,"Article Count","기사를 기다리고 있습니다",.025f,0,.31f,.05f,24,Muted);
        listScroll = Scroll(reading.transform,"Article List",.025f,.06f,.31f,.93f,out listContent);
        articleScroll = Scroll(reading.transform,"Article Detail",.375f,0,.60f,.90f,out articleContent);
        Panel(reading.transform,"News Divider",.352f,0,.001f,1,Line);
        empty = Label(reading.transform,"Empty News","조건에 맞는 기사가 없습니다",.035f,.18f,.29f,.18f,30,Muted);
        articleText = Label(articleContent,"Article Text","",0,0,1,1,32,Ink);
        articleText.richText = true; articleText.overflowMode = TextOverflowModes.Overflow;
        articleText.textWrappingMode = TextWrappingModes.Normal; articleText.lineSpacing = 7;
        order = Button(reading.transform,"Article Order","이 종목 거래",.375f,.93f,.30f,.07f,OpenOrder);
        TabletControlStyle.Emphasize(order,true);
        Button(reading.transform,"Related Articles","관련 기사",.695f,.93f,.28f,.07f,() =>
        { if (selected != null) { query = selected.company; search.SetTextWithoutNotify(query); mode = "all"; category = "all"; categoryIndex = -1; RefreshList(); } });
        // Reuse the old event row pool. All item selection is mapped to the currently filtered article.
        for (int i = 0; i < terminal.eventButtons.Length; i++)
        {
            int index = i; var button = terminal.eventButtons[i]; button.transform.SetParent(listContent,false);
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => SelectRow(index));
            button.name = "Button :: News Row " + (i + 1); button.image.sprite = null; button.image.type = Image.Type.Simple;
            button.image.color = Color.clear; button.transition = Selectable.Transition.ColorTint;
            var title = terminal.eventTitles[i]; var details = terminal.eventTags[i];
            Style(title,30,Ink,true); Style(details,25,Muted,false);
            Rect(title.rectTransform,.04f,.07f,.92f,.56f); title.textWrappingMode = TextWrappingModes.Normal;
            Rect(details.rectTransform,.04f,.69f,.92f,.29f); details.textWrappingMode = TextWrappingModes.Normal;
            Panel(button.transform,"Article Row Divider",0,.99f,1,.005f,Line);
            rows.Add(button); titles.Add(title); meta.Add(details);
        }
        Button(transform,"News Management","기업 경영",.025f,.94f,.19f,.045f,() => navigation.ShowPage(TabletAccountUI.Page.Management));
        Button(transform,"News Growth","성장·권한",.225f,.94f,.19f,.045f,() => terminal.growth.onClick.Invoke());
        Button(transform,"Market Alerts","알림 기록",.435f,.94f,.19f,.045f,() => SetMode("alerts"));
        if (navigation.second)
        { studioButton = Button(transform,"News Studio","기사 만들기",.73f,.94f,.245f,.045f,OpenStudio); }
        BuildStudio(); RefreshList();
    }
    private void SetMode(string value) { mode = value; RefreshList(); }
    void OnEnable() { version = -1; next = 0; }
    private void ChooseCategory()
    {
        var catalog = NewsManager.Instance?.Writing;
        if (catalog == null) { return; }
        var choices = new string[catalog.narratives.Length + 1]; choices[0] = "전체 분야";
        for (int i = 0; i < catalog.narratives.Length; i++) { choices[i + 1] = catalog.narratives[i].label; }
        Choose(choices,categoryIndex + 1,.655f,.16f,.32f,index =>
        { categoryIndex = index - 1; category = index == 0 ? "all" : catalog.narratives[categoryIndex].key; mode = mode == "alerts" ? "all" : mode; RefreshList(); });
    }
    private void Choose(string[] options, int selectedIndex, float x, float y, float width, Action<int> select)
    {
        if (picker != null) { Destroy(picker); }
        picker = Area(transform,"News Options",0,0,1,1).gameObject;
        var outside = Panel(picker.transform,"Close Options",0,0,1,1,new Color(0,0,0,.20f)); outside.raycastTarget = true;
        var close = outside.gameObject.AddComponent<Button>(); close.targetGraphic = outside;
        close.onClick.AddListener(() => Destroy(picker));
        var menu = Area(picker.transform,"News Options List",x,y,width,Math.Min(options.Length * .060f,.97f-y));
        Panel(menu,"Options Background",0,0,1,1,new Color32(25,36,43,255));
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            var button = Button(menu,"Option " + i,options[i],.025f,(float)i/options.Length,.95f,1f/options.Length,() => { Destroy(picker); select(index); });
            TabletControlStyle.Emphasize(button,i == selectedIndex);
        }
    }
    void OnDisable() { if (picker != null) { Destroy(picker); } }
    private void RefreshList()
    {
        var news = NewsManager.Instance;
        if (news == null || navigation == null) { return; }
        float scroll = listScroll.verticalNormalizedPosition;
        version = news.ArticleVersion; alertVersion = terminal.records.Version; visible.Clear();
        var stock = StockManager.Instance?.CurrentStock;
        if (mode == "alerts") { RefreshAlerts(); }
        else { foreach (var article in news.Articles)
        {
            var relatedStock = FindStock(article.symbol);
            bool held = relatedStock != null && navigation.account.GetOwnedVolume(relatedStock) > 0;
            if (NewsWriting.Matches(article,query,category,mode == "current" ? stock?.symbol : null,mode == "owned",held,mode == "rumors"))
            { visible.Add(article); }
        } }
        if (selected == null || !visible.Contains(selected)) { selected = visible.Count > 0 ? visible[0] : null; articleScroll.verticalNormalizedPosition = 1; }
        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i < visible.Count; rows[i].gameObject.SetActive(active); if (!active) { continue; }
            var rect = (RectTransform)rows[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchorMax = new Vector2(1,1); rect.anchoredPosition = new Vector2(0,-i * 157); rect.sizeDelta = new Vector2(0,156);
            var article = visible[i]; Set(titles[i],article.title);
            Set(meta[i],(string.IsNullOrEmpty(article.company) ? article.source : article.company) + "  ·  " + article.time +
                (article.credibility < .5 ? " · 미확인" : article.correction ? " · 후속" : ""));
            rows[i].image.color = article == selected ? Selected : Color.clear;
        }
        listContent.sizeDelta = new Vector2(0,visible.Count * 157); listScroll.verticalNormalizedPosition = scroll;
        empty.gameObject.SetActive(visible.Count == 0); Set(countText, (mode == "alerts" ? "시장 알림 " : "최근 기사 ") + visible.Count);
        TabletControlStyle.Emphasize(all,mode == "all"); TabletControlStyle.Emphasize(owned,mode == "owned");
        TabletControlStyle.Emphasize(current,mode == "current"); TabletControlStyle.Emphasize(rumors,mode == "rumors");
        Text(categoryButton,"분야 · " + (categoryIndex < 0 ? "전체" : news.Writing.narratives[categoryIndex].label) + " ▼");
        RenderArticle();
    }
    private void RefreshAlerts()
    {
        var removed = new List<DeviceMarketJournal.Entry>();
        foreach (var entry in alerts.Keys) { if (!terminal.records.Events.Contains(entry)) { removed.Add(entry); } }
        foreach (var entry in removed) { alerts.Remove(entry); }
        foreach (var entry in terminal.records.Events)
        {
            if (entry.Article != null) { continue; }
            if (!alerts.TryGetValue(entry, out var article))
            {
                article = new NewsArticle { symbol = entry.Stock?.symbol, company = entry.Stock?.stockName, title = entry.Title,
                    category = "notice", categoryLabel = "시장 알림", source = entry.Source, verification = entry.Certainty,
                    time = entry.Time, credibility = 1, summary = entry.Certainty,
                    body = entry.Stock == null ? "게임 내 시장과 진행 상태에 관한 알림입니다." :
                        "발생 직전 60초 · 실제 체결 " + entry.Volume.ToString("N0") + "주\n매수 주도 비중 " +
                        (entry.Volume > 0 ? (entry.BuyVolume / entry.Volume * 100).ToString("0.0") + "%" : "집계 없음") +
                        "\n매도 호가를 소진한 체결량 / 전체 체결량입니다." };
                alerts.Add(entry, article);
            }
            if (NewsWriting.Matches(article,query,"all",null,false,false,false)) { visible.Add(article); }
        }
    }
    private void SelectRow(int row)
    {
        if (row < 0 || row >= visible.Count) { return; }
        selected = visible[row]; articleScroll.verticalNormalizedPosition = 1;
        foreach (var button in rows) { button.image.color = Color.clear; }
        rows[row].image.color = Selected; RenderArticle();
    }
    private static string Safe(string value) => (value ?? "").Replace("<","‹").Replace(">","›");
    private void RenderArticle()
    {
        order.interactable = selected != null && FindStock(selected.symbol)?.AcceptsOrders == true;
        if (selected == null) { SetArticle("아직 읽을 기사가 없습니다.\n\n필터를 바꾸거나 새 소식을 기다려 주세요."); return; }
        buffer.Clear();
        buffer.Append("<size=24><color=#99A8B3>").Append(Safe(selected.categoryLabel)).Append("  ·  ").Append(Safe(selected.source)).Append("  ·  ").Append(selected.time)
            .Append("</color></size>\n\n<size=44><b>").Append(Safe(selected.title)).Append("</b></size>\n\n")
            .Append("<color=#76CAD4>").Append(Safe(selected.company ?? "시장")).Append(" · ").Append(Safe(selected.verification)).Append("</color>\n\n")
            .Append(Safe(selected.summary)).Append("\n\n");
        string body = Safe(selected.body);
        foreach (string heading in new[] {"무슨 일이 있었나","전해진 주장","후속 확인","배경과 이해관계","사회가 받아들이는 방식","다음 확인 지점"})
        { body = body.Replace(heading + "\n","<b>" + heading + "</b>\n"); }
        buffer.Append(body);
        if (selected.links.Length > 0)
        {
            buffer.Append("\n\n<b>관련 기업 · 연결고리</b>\n");
            foreach (var link in selected.links)
            {
                var company = FindStock(link.symbol); if (company == null) { continue; }
                buffer.Append(Safe(company.stockName)).Append(" — ").Append(Safe(link.relation)).Append('\n');
            }
        }
        if (selected.correction)
        {
            var original = NewsManager.Instance.FindArticle(selected.storyId);
            if (original != null) { buffer.Append("\n\n<b>앞선 보도</b>\n").Append(Safe(original.title)); }
        }
        var entry = terminal.records.Events.Find(row => row.Id == selected.id && row.Stock != null);
        if (entry != null)
        {
            buffer.Append("\n\n<size=26><color=#99A8B3>보도 직전 60초 · 실제 체결 ").Append(entry.Volume.ToString("N0")).Append("주\n매수 주도 비중 ")
                .Append(entry.Volume > 0 ? (entry.BuyVolume / entry.Volume * 100).ToString("0.0") + "%" : "집계 없음").Append("</color></size>");
        }
        if (selected.id > 0) { buffer.Append("\n\n<b>라운지 반응</b>\n").Append(Safe(CompanyNewsAudience.Instance?.Responses(selected.id) ?? "연결된 공개 발언이 아직 없습니다.")); }
        SetArticle(buffer.ToString());
    }
    private void SetArticle(string text)
    {
        float previous = articleScroll.verticalNormalizedPosition; Set(articleText,text);
        float width = Math.Max(100,articleScroll.viewport.rect.width - 16);
        float height = articleText.GetPreferredValues(text,width,0).y + 40;
        articleContent.sizeDelta = new Vector2(0,height);
        articleText.rectTransform.anchorMin = new Vector2(0,1); articleText.rectTransform.anchorMax = Vector2.one;
        articleText.rectTransform.pivot = new Vector2(0,1); articleText.rectTransform.anchoredPosition = Vector2.zero;
        articleText.rectTransform.sizeDelta = new Vector2(-16,height);
        articleScroll.verticalNormalizedPosition = previous;
    }
    private void OpenOrder()
    {
        var stock = selected == null ? null : FindStock(selected.symbol);
        if (stock == null || !stock.AcceptsOrders) { return; }
        StockManager.Instance.SelectStock(StockManager.Instance.stocks.IndexOf(stock)); navigation.ShowPage(TabletAccountUI.Page.Orders);
    }
    private void BuildStudio()
    {
        studio = Area(transform,"News Studio",.025f,.17f,.95f,.75f).gameObject;
        Panel(studio.transform,"Studio Background",0,0,1,1,Surface);
        Label(studio.transform,"Studio Heading","가짜 뉴스 발행",0,0,.70f,.075f,40,Ink,true);
        Button(studio.transform,"Close Studio","기사 목록",.79f,0,.21f,.065f,() => { studio.SetActive(false); reading.SetActive(true); });
        draftStock = Button(studio.transform,"Draft Stock","대상 종목",0,.10f,.46f,.07f,ChooseStock);
        draftCategory = Button(studio.transform,"Draft Theme","분야",.49f,.10f,.51f,.07f,() =>
        {
            var catalog = NewsManager.Instance.Writing; var options = new string[catalog.narratives.Length];
            for (int i = 0; i < options.Length; i++) { options[i] = catalog.narratives[i].label; }
            Choose(options,draftCategoryIndex,.50f,.31f,.475f,index => { draftCategoryIndex = index; RefreshDraft(); });
        });
        positive = Button(studio.transform,"Positive Draft","호재 방향",0,.20f,.24f,.06f,() => { optimistic = true; RefreshDraft(); });
        negative = Button(studio.transform,"Negative Draft","악재 방향",.26f,.20f,.24f,.06f,() => { optimistic = false; RefreshDraft(); });
        Button(studio.transform,"Another Draft","다른 초안",.74f,.20f,.26f,.06f,() => { variant++; RefreshDraft(); });
        var previewScroll = Scroll(studio.transform,"Draft Preview",0,.30f,1,.47f,out var previewContent);
        draftText = Label(previewContent,"Draft Text","",0,0,1,1,31,Ink); draftText.richText = true; draftText.overflowMode = TextOverflowModes.Overflow;
        draftText.textWrappingMode = TextWrappingModes.Normal; draftText.lineSpacing = 6;
        cooldownText = Label(studio.transform,"Publication Cooldown","",0,.80f,.65f,.055f,27,Muted);
        resultText = Label(studio.transform,"Publication Result","",0,.88f,.65f,.11f,28,Ink);
        publish = Button(studio.transform,"Publish Draft","이 초안 발행",.70f,.85f,.30f,.12f,Publish);
        TabletControlStyle.Emphasize(publish,true);
        studio.SetActive(false);
    }
    private void OpenStudio()
    {
        if (!navigation.second || navigation.Locked) { return; }
        reading.SetActive(false); studio.SetActive(true); RefreshDraft();
    }
    private void ChooseStock()
    {
        var manager = StockManager.Instance;
        if (manager == null || manager.stocks.Count == 0) { return; }
        var stocks = manager.stocks.ToArray(); var options = new string[stocks.Length];
        for (int i = 0; i < stocks.Length; i++) { options[i] = stocks[i].stockName; }
        Choose(options,manager.stocks.IndexOf(manager.CurrentStock),.025f,.31f,.437f,index =>
        { int currentIndex = manager.stocks.IndexOf(stocks[index]); if (currentIndex >= 0) { manager.SelectStock(currentIndex); RefreshDraft(); } });
    }
    private void RefreshDraft()
    {
        previewStock = StockManager.Instance?.CurrentStock;
        var publisher = FakeNewsManager.Instance; var news = NewsManager.Instance;
        if (publisher == null || news == null || previewStock == null) { return; }
        var theme = news.Writing.narratives[draftCategoryIndex];
        draft = publisher.PreviewPlayer(previewStock,theme.key,optimistic,variant);
        Text(draftStock,"대상 · " + previewStock.stockName + " ▼"); Text(draftCategory,"분야 · " + theme.label + " ▼");
        TabletControlStyle.Emphasize(positive,optimistic); TabletControlStyle.Emphasize(negative,!optimistic);
        string text = "<b>" + Safe(draft.title) + "</b>\n\n" + Safe(draft.summary) + "\n\n" + Safe(draft.body);
        Set(draftText,text); var content = (RectTransform)draftText.transform.parent;
        float width = Math.Max(100,((RectTransform)content.parent).rect.width - 16);
        float height = draftText.GetPreferredValues(text,width,0).y + 30; content.sizeDelta = new Vector2(0,height);
        Rect(draftText.rectTransform,0,0,1,1); draftText.rectTransform.offsetMax = new Vector2(-16,0);
        if (Time.unscaledTime >= resultUntil) { Set(resultText,"공개되면 투자자마다 다르게 받아들입니다.\n후속 확인 보도가 나올 수 있습니다."); }
    }
    private void Publish()
    {
        if (draft == null || !navigation.second || navigation.Locked) { return; }
        // Publish the previewed company, even if the user changes global selection before this frame.
        var publisher = FakeNewsManager.Instance;
        if (publisher == null) { return; }
        publisher.TryPublishPlayer(previewStock,draft.category,optimistic,variant,out var article,out string reason);
        Set(resultText,reason); resultUntil = Time.unscaledTime + 8;
        if (article != null) { selected = article; version = -1; }
        next = 0;
    }
    void Update()
    {
        if (navigation == null || navigation.Locked || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .3f;
        var news = NewsManager.Instance; if (news == null) { return; }
        var stock = StockManager.Instance?.CurrentStock;
        if (version != news.ArticleVersion || stock != lastStock && mode == "current" || mode == "alerts" && alertVersion != terminal.records.Version) { RefreshList(); }
        lastStock = stock;
        int responses = CompanyNewsAudience.Instance == null ? 0 : CompanyNewsAudience.Instance.Version;
        if (responses != responseVersion) { responseVersion = responses; RenderArticle(); }
        if (!studio.activeSelf) { return; }
        if (stock != previewStock) { RefreshDraft(); }
        var publisher = FakeNewsManager.Instance;
        double remaining = publisher == null ? 0 : publisher.PlayerCooldown;
        Set(cooldownText,remaining > 0 ? "다음 발행까지 " + FakeNewsManager.FormatCooldown(remaining) + " · 현실시간" : "발행 가능 · 발행 후 현실시간 10분 대기");
        publish.interactable = publisher != null && publisher.PlayerUnlocked && remaining <= 0 && MarketDayController.CanTrade && previewStock?.AcceptsOrders == true;
        Text(publish,remaining > 0 ? "대기 " + FakeNewsManager.FormatCooldown(remaining) : "이 초안 발행");
    }
    private static Stock FindStock(string symbol)
    {
        if (StockManager.Instance == null) { return null; }
        foreach (var stock in StockManager.Instance.stocks) { if (stock.symbol == symbol && !stock.IsDelisted) { return stock; } }
        return null;
    }
    private static void Set(TextMeshProUGUI label,string value) { DeviceTerminalUI.Set(label,value); }
    private static void Text(Button button,string value) { Set(button.GetComponentInChildren<TextMeshProUGUI>(),value); }
    private static void Rect(RectTransform rect,float x,float y,float w,float h)
    {
        rect.anchorMin = new Vector2(x,1-y-h); rect.anchorMax = new Vector2(x+w,1-y);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
    private static RectTransform Area(Transform parent,string name,float x,float y,float w,float h)
    {
        var root = new GameObject("Area :: " + name,typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent,false); Rect(root,x,y,w,h); return root;
    }
    private static Image Panel(Transform parent,string name,float x,float y,float w,float h,Color color)
    {
        var rect = Area(parent,name,x,y,w,h); rect.name = "Image :: " + name;
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private void Style(TextMeshProUGUI text,float size,Color color,bool bold)
    {
        text.font = font; text.fontSharedMaterial = font.material; text.fontSize = size; text.enableAutoSizing = false;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; text.color = color; text.margin = Vector4.zero;
        text.alignment = TextAlignmentOptions.TopLeft; text.raycastTarget = false; text.richText = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
    }
    private TextMeshProUGUI Label(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color,bool bold = false)
    {
        var rect = Area(parent,name,x,y,w,h); rect.name = "Text :: " + name;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); Style(label,size,color,bold); label.text = text; return label;
    }
    private Button Button(Transform parent,string name,string caption,float x,float y,float w,float h,Action action)
    {
        var background = Panel(parent,name,x,y,w,h,Surface); background.name = "Button :: " + name; background.raycastTarget = true;
        var button = background.gameObject.AddComponent<Button>(); button.targetGraphic = background;
        var label = Label(background.transform,name + " Label",caption,.025f,0,.95f,1,28,Ink);
        label.alignment = TextAlignmentOptions.Center; button.onClick.AddListener(() => action()); TabletControlStyle.Button(button); return button;
    }
    private ScrollRect Scroll(Transform parent,string name,float x,float y,float w,float h,out RectTransform content)
    {
        var root = Area(parent,name,x,y,w,h); var image = root.gameObject.AddComponent<Image>(); image.color = new Color(0,0,0,0); image.raycastTarget = true;
        root.gameObject.AddComponent<RectMask2D>();
        var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.viewport = root; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 70;
        content = Area(root,name + " Content",0,0,1,1); content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
        content.anchoredPosition = Vector2.zero; scroll.content = content;
        var track = Panel(root,"Scroll Track",.989f,0,.011f,1,new Color32(36,48,55,255)); track.raycastTarget = true;
        var thumb = Panel(track.transform,"Scroll Thumb",0,0,1,1,new Color32(109,133,146,220)); thumb.raycastTarget = true;
        var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.targetGraphic = thumb;
        scrollbar.handleRect = (RectTransform)thumb.transform; scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }
    private TMP_InputField Input(Transform parent,float x,float y,float w,float h)
    {
        var rect = Area(parent,"News Search",x,y,w,h); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(25,36,43,255);
        var field = rect.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic = image;
        var text = Label(rect,"Search Text","",.04f,.08f,.92f,.84f,28,Ink); text.raycastTarget = true;
        var placeholder = Label(rect,"Search Placeholder","기업·제목 검색",.04f,.08f,.92f,.84f,28,Muted);
        field.textViewport = rect; field.textComponent = text; field.placeholder = placeholder; field.characterLimit = 60;
        field.lineType = TMP_InputField.LineType.SingleLine; TabletControlStyle.Input(field); return field;
    }
}
```

### Assets/Scripts/NightShift/ChatSocialCompanyNews.cs

```csharp
using System;

public sealed partial class InvestorSociety
{
    public void OfferCompanyNews(TradeBot bot, Stock stock, NewsArticle article, double interpretedSentiment)
    {
        if (bot?.Social == null || stock == null || article == null || !Present(bot, bot.Social.Psychology.Room)) { return; }
        var mind = bot.Social.Psychology;
        var facts = CaptureFacts(bot, stock);
        facts.companyNewsId = article.id; facts.newsHeadline = article.title; facts.newsObserver = bot.Social.Id;
        facts.companySentiment = interpretedSentiment; facts.newsCorrection = article.correction;
        facts.notice = "COMPANY_NEWS_" + mind.Companies.Stance(stock.symbol, interpretedSentiment, article.correction);
        facts.eventId = ChatSocialIds.Next();
        Offer(bot, mind.Room, SocialMotive.SELF_EVENT, facts, SocialAct.REACT, cause: facts.eventId);
    }
    public static bool IsCompanyNews(string notice) => notice != null && notice.StartsWith("COMPANY_NEWS_", StringComparison.Ordinal);
    private bool PreserveCompanyNews(TradeBot bot, SocialIntent intent, SocialFacts facts)
    {
        var old = intent.facts;
        if (old == null || !IsCompanyNews(old.notice)) { return true; }
        facts.companyNewsId = old.companyNewsId; facts.newsHeadline = old.newsHeadline;
        facts.companySentiment = old.companySentiment; facts.newsCorrection = old.newsCorrection;
        if (intent.replyToId != 0 || old.newsObserver != bot.Social.Id)
        { facts.notice = null; return true; }
        var opinion = bot.Social.Psychology.Companies.Read(facts.symbol);
        if (opinion == null || opinion.lastNews != old.companyNewsId) { return false; }
        facts.newsObserver = old.newsObserver;
        facts.notice = "COMPANY_NEWS_" + bot.Social.Psychology.Companies.Stance(facts.symbol, old.companySentiment, old.newsCorrection);
        return true;
    }
    private void WitnessCompanyOpinion(TradeBot witness, SocialMessage message)
    {
        var facts = message.facts;
        if (witness?.Social == null || facts == null || !IsCompanyNews(facts.notice) || facts.companyNewsId <= 0) { return; }
        double stance = facts.notice == "COMPANY_NEWS_FAN" || facts.notice == "COMPANY_NEWS_DEFEND" ? 1 :
            facts.notice == "COMPANY_NEWS_DOUBT" || facts.notice == "COMPANY_NEWS_CAUTION" ? -1 : Math.Sign(facts.companySentiment) * .5;
        if (witness.Social.Psychology.Companies.Hear(facts.symbol, facts.companyNewsId, stance, SocialTime.Seconds))
        {
            ChatSocialAudit.Journal.Event(new SocialEvent { id = facts.companyNewsId, time = SocialTime.GameMinute, actor = witness.Social.Id,
                target = message.senderId, room = message.roomId, kind = "COMPANY_OPINION_HEARD", reason = facts.symbol, value = stance });
        }
    }
}
```

### Assets/Scripts/NightShift/TabletControlStyle.cs

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Generated bitmap surfaces for actions and editable fields; descriptions remain ordinary TMP text.</summary>
public static class TabletControlStyle
{
    private static Sprite primary, secondary, input;
    private static TMP_FontAsset readingFont;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { primary = secondary = input = null; readingFont = null; }
    public static Sprite Primary => primary ??= Load("ActionPrimary-v1",new Rect(0,104,2172,524));
    public static Sprite Secondary => secondary ??= Load("ActionSecondary-v1",new Rect(0,104,2172,524));
    public static Sprite InputSurface => input ??= Load("InputField-v1",new Rect(0,84,2172,556));
    private static Sprite Load(string name, Rect sourceRect)
    {
        var source = Resources.Load<Sprite>("UI/TabletControls/" + name);
        if (source == null) { return null; }
        // Keep the generated PNG intact. A cached sprite rectangle removes transparent export padding.
        float sx = source.texture.width / 2172f, sy = source.texture.height / 724f;
        var rect = new Rect(sourceRect.x*sx,sourceRect.y*sy,sourceRect.width*sx,sourceRect.height*sy);
        var sprite = Sprite.Create(source.texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(80*sx,80*sy,80*sx,80*sy));
        sprite.name = name + " Sliced"; sprite.hideFlags = HideFlags.DontSave; return sprite;
    }
    private static void Surface(Image image, Sprite sprite)
    {
        if (image == null || sprite == null) { return; }
        if (image.sprite != sprite) { image.sprite = sprite; }
        image.type = Image.Type.Sliced; image.fillCenter = true; image.preserveAspect = false;
        image.pixelsPerUnitMultiplier = 4; image.color = Color.white;
    }
    public static void Emphasize(Button button, bool active)
    {
        if (button == null) { return; }
        Sprite sprite = active ? Primary : Secondary;
        if (button.image != null && (button.image.sprite != sprite || button.image.color != Color.white)) { Surface(button.image,sprite); }
    }
    public static void Button(Button button, bool primaryAction = false)
    {
        if (button == null) { return; }
        Surface(button.image,primaryAction ? Primary : Secondary);
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.18f,1.18f,1.18f,1);
        colors.pressedColor = new Color(.70f,.75f,.78f,1); colors.selectedColor = new Color(1.08f,1.08f,1.08f,1);
        colors.disabledColor = new Color(.62f,.65f,.68f,.45f); colors.fadeDuration = .10f; colors.colorMultiplier = 1;
        button.colors = colors;
        foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
        { Font(label); label.fontStyle = FontStyles.Bold; label.raycastTarget = false; }
    }
    public static void Input(TMP_InputField field)
    {
        if (field == null) { return; }
        Surface(field.targetGraphic as Image,InputSurface); field.transition = Selectable.Transition.ColorTint;
        var colors = field.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.9f,1.08f,1.12f,1);
        colors.selectedColor = new Color(.65f,1.20f,1.28f,1); colors.pressedColor = colors.selectedColor;
        colors.disabledColor = new Color(.65f,.65f,.65f,.45f); colors.fadeDuration = .12f; colors.colorMultiplier = 1; field.colors = colors;
        field.customCaretColor = true; field.caretColor = new Color32(111,219,232,255); field.caretWidth = 2;
        Font(field.textComponent); if (readingFont != null) { field.fontAsset = readingFont; }
        field.textComponent.fontStyle = FontStyles.Normal;
        if (field.placeholder is TMP_Text hint) { Font(hint); hint.color = new Color32(138,158,171,255); }
    }
    private static void Font(TMP_Text text)
    {
        if (text == null) { return; }
        readingFont ??= Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        if (readingFont == null) { return; }
        text.font = readingFont; text.fontSharedMaterial = readingFont.material;
    }
    public static void Apply(TabletAccountUI tablet)
    {
        var tabs = new HashSet<Button>(tablet.tabs);
        foreach (var terminal in new[] { tablet.orders,tablet.events })
        {
            tabs.Add(terminal.mainTab); tabs.Add(terminal.legacy.deskTab); tabs.Add(terminal.legacy.legacyTab);
            if (terminal.eventButtons != null) { foreach (var row in terminal.eventButtons) { tabs.Add(row); } }
        }
        foreach (var label in tablet.contentRoot.GetComponentsInChildren<TMP_Text>(true)) { Font(label); }
        foreach (var button in tablet.contentRoot.GetComponentsInChildren<Button>(true))
        {
            if (tabs.Contains(button) || button.GetComponent<InvestmentPositionRow>() != null || button.GetComponentInParent<NewsDeskUI>(true) != null) { continue; }
            Button(button);
        }
        foreach (var field in tablet.contentRoot.GetComponentsInChildren<TMP_InputField>(true)) { Input(field); }
        foreach (var button in new[] { tablet.orderButton,tablet.transferSubmit,tablet.orders.review,tablet.orders.submit,tablet.events.openRooms })
        { Button(button,true); }
    }
}
```

### Assets/Editor/NewsFontSetup.cs

```csharp
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Creates the news reader's dedicated SDF assets without touching scenes or other fonts.</summary>
public static class NewsFontSetup
{
    private const string Folder = "Assets/Resources/UI/News";
    [MenuItem("DonDon/UI/Prepare News Reading Font")]
    public static void Ensure()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var regular = Create("Pretendard Reading", "Regular");
        var bold = Create("Pretendard Semibold", "SemiBold");
        if (regular.fontWeightTable[7].regularTypeface != bold)
        {
            regular.fontWeightTable[7] = new TMP_FontWeightPair { regularTypeface = bold, italicTypeface = bold };
            EditorUtility.SetDirty(regular);
        }
        AssetDatabase.SaveAssets();
    }
    private static TMP_FontAsset Create(string name, string weight)
    {
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) { return existing; }
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/News/Pretendard-" + weight + ".otf");
        if (source == null) { throw new FileNotFoundException("Pretendard source font missing: " + weight); }
        var font = TMP_FontAsset.CreateFontAsset(source,48,6,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic);
        font.name = name; font.isMultiAtlasTexturesEnabled = true;
        font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Chat/Chat Korean.asset");
        if (fallback != null) { font.fallbackFontAssetTable.Add(fallback); }
        AssetDatabase.CreateAsset(font,path);
        AssetDatabase.AddObjectToAsset(font.material,font);
        foreach (var texture in font.atlasTextures) { AssetDatabase.AddObjectToAsset(texture,font); }
        string corpus = File.ReadAllText("Assets/Resources/News/news_writing.json") + "시장뉴스기사목록알림기록성장권한최근전체보유선택미확인발행현실시간다음대기검색완료1234567890:+-·%";
        font.TryAddCharacters(new string(corpus.Distinct().ToArray()));
        EditorUtility.SetDirty(font);
        return font;
    }
}
```

### Assets/Editor/TabletControlSpriteSetup.cs

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Import individual imagegen assets without replacing existing UI sprites.</summary>
public static class TabletControlSpriteSetup
{
    [MenuItem("DonDon/UI/Import Tablet Control Sprites")]
    public static void Ensure()
    {
        foreach (string name in new[] { "ActionPrimary-v1", "ActionSecondary-v1", "InputField-v1" })
        {
            string path = "Assets/Resources/UI/TabletControls/" + name + ".png";
            if (!File.Exists(path)) { throw new FileNotFoundException(path); }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 1024;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
    }
}
```

### Tests/ChatSocial/TestsNewsSociety.cs

```csharp
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static void NewsSocietyScenarios()
    {
        var catalog = JsonUtility.FromJson<NewsWritingCatalog>(File.ReadAllText(Path.Combine(root,"Assets/Resources/News/news_writing.json")));
        Check(catalog.narratives.Length == 9 && catalog.companies.Length >= 8,"nine news subjects and explicit fictional company links load from production data");
        Check(NewsWriting.Classify(catalog,"첫 대형 수주 계약 체결 - 실적 기대감 상승")=="contracts" && NewsWriting.Classify(catalog,"유동성 우려 - 자금 조달 실패")=="finance","classification follows the headline's main event before secondary consequences");
        var bodies = Enumerable.Range(1,80).Select(id => NewsWriting.Compose(catalog,id,"NM","노보손해보험","신규 계약 체결",.5,"09:00",0)).ToArray();
        Check(bodies.Select(a => a.body).Distinct().Count() >= 30 && bodies.All(a => !a.body.Contains("{")),"same headline has varied contextual bodies without unresolved placeholders");
        Check(bodies[0].body == NewsWriting.Compose(catalog,1,"NM","노보손해보험","신규 계약 체결",.5,"09:00",0).body,"article generation is deterministic and independent of market RNG");
        foreach (var category in catalog.narratives)
        {
            string headline = NewsWriting.RumorTitle(catalog,"NM","노보손해보험",category.key,false,4);
            var preview = NewsWriting.Compose(catalog,0,"NM","노보손해보험",headline,-.5,"미리보기",0,true,category.key,4);
            var published = NewsWriting.Compose(catalog,900,"NM","노보손해보험",headline,-.5,"10:00",60,true,category.key,4);
            Check(preview.body == published.body && preview.title == published.title && preview.credibility < .5,"preview remains the exact published draft: " + category.key);
        }
        var rumor = NewsWriting.Compose(catalog,101,"NM","노보손해보험","계약설",.5,"10:00",0,true,"contracts");
        var correction = NewsWriting.Compose(catalog,102,"NM","노보손해보험","계약설 근거 확인 안돼",-.5,"10:02",120,false,"contracts",0,101,true);
        Check(correction.storyId == rumor.id && correction.credibility > rumor.credibility && correction.verification == "후속 확인","correction retains the original story and stronger public evidence");
        Check(NewsWriting.Matches(rumor,"노보","contracts","NM",true,true,true) && !NewsWriting.Matches(rumor,"노보","contracts","DDP",false,true,false),"combined search, category, selected stock, holdings and unconfirmed filters use actual article fields");
        Check(!NewsWriting.Matches(rumor,"","all",null,true,false,false) && !NewsWriting.Matches(correction,"","all",null,false,false,true),"empty holdings and corrected news are excluded by their respective filters");

        var fan = new CompanyOpinionBook(73129,1); var skeptical = new CompanyOpinionBook(73129,2);
        fan.Read("NM").affinity = 80; skeptical.Read("NM").affinity = 0;
        fan.Read("NM").skepticism = skeptical.Read("NM").skepticism = 20;
        fan.Observe("NM",1,-1,.32,false,.7,true,0); skeptical.Observe("NM",1,-1,.32,false,.7,true,0);
        Check(fan.Read("NM").impulse > 0 && skeptical.Read("NM").impulse < 0 && fan.Stance("NM",-1,false) == "DEFEND","fans can defend a company against an unsubstantiated negative story while neutral readers worry");
        double affinity = fan.Read("NM").affinity;
        for (int id = 2; id <= 8; id++) { fan.Observe("NM",id,-1,.95,id==8,.7,true,id); }
        Check(fan.Read("NM").affinity < affinity - 20 && fan.Read("NM").disappointments == 7 && fan.Read("NM").skepticism == 28,"repeated reliable bad news erodes attachment; corrections also raise skepticism");
        affinity = fan.Read("NM").affinity;
        Check(!fan.Observe("NM",8,-1,.95,true,.7,true,9) && fan.Read("NM").affinity == affinity && !fan.Hear("NM",8,-1,9),"one article cannot be counted again through reading or chat");
        var listener = new CompanyOpinionBook(73129,3); affinity = listener.Read("NM").affinity;
        Check(listener.Hear("NM",8,-1,9) && listener.Read("NM").affinity == affinity-1 && !listener.Hear("NM",8,-1,10),"hearsay is weaker than firsthand evidence and repeated hearsay is bounded");
        Check(listener.Read("DDP").lastNews == 0 && listener.TradeScale("UNSEEN",true,10) == 1,"unobserved companies receive no news impulse");
        double active = Math.Abs(skeptical.TradeScale("NM",true,0)-1);
        Check(Math.Abs(skeptical.TradeScale("NM",true,900)-1) < active && fan.TradeScale("NM",false,8) <= 1.35 && fan.TradeScale("NM",true,8) >= .65,"news trading bias decays and order sizing stays bounded");
        var whole = new CompanyOpinionBook(42,5); var split = new CompanyOpinionBook(42,5);
        affinity = whole.Read("NM").affinity;
        whole.Realized("NM",500,1000,10);
        for (int i=0; i<10; i++) { split.Realized("NM",50,100,10+i); }
        Check(whole.Read("NM").affinity == split.Read("NM").affinity && whole.Read("NM").affinity > affinity,"actual realized profits improve attachment without multiplying split-fill rewards");
        whole.Realized("NM",-500,1000,70);
        Check(whole.Read("NM").affinity == affinity,"actual losses can reverse earlier favorable experience");
        whole.Realized("NM",5,double.NaN,71);
        Check(CompanyOpinionBook.Valid(whole.Capture()) && !whole.Observe("NM",9,double.NaN,1,false,.5,false,2),"invalid price math cannot poison company memory");
        var snapshot = fan.Capture(); var restored = new CompanyOpinionBook(7,7);
        Check(restored.Restore(snapshot) && !restored.Observe("NM",8,-1,.9,false,.5,false,9),"company memory and seen-event protection survive snapshot restore");
        snapshot[0].affinity = 99; snapshot[0].seen.Clear();
        Check(restored.Read("NM").affinity != 99 && restored.Read("NM").seen.Count > 0,"restored opinion data is not aliased to a mutable save object");
        Check(restored.Restore(null) && restored.Capture().Length == 0,"old saves without company opinions remain valid");
        var population = Enumerable.Range(1,200).Select(id => new CompanyOpinionBook(73129,id)).ToArray();
        for (int i=1;i<=120;i++)
        {
            foreach (var book in population)
            { if (NewsWriting.Hash(i+":"+book.Read("NM").conviction) % 4 == 0) { book.Observe("NM",i,i%2==0?1:-1,.7,false,.5,false,i*30); } }
        }
        Check(population.All(b => CompanyOpinionBook.Valid(b.Capture())) && population.Max(b=>b.Read("NM").affinity)-population.Min(b=>b.Read("NM").affinity)>30,"one-hour mixed evidence model preserves diverse bounded opinions");

        var clock = new NewsPublicationClock();
        Check(clock.TryConsume(100) && !clock.TryConsume(100) && !clock.TryConsume(699.999) && clock.TryConsume(700),"publication opens exactly after 600 real seconds, never on repeated submit");
        Check(!clock.TryConsume(double.NaN) && clock.Remaining(701)==599,"invalid timestamps cannot consume or clear a cooldown");
        Check(clock.Restore(500,120,10) && clock.Remaining(10)==380 && clock.Remaining(15)==375,"restored cooldown subtracts offline time and advances by real seconds");
        Check(!clock.Restore(601,0,10) && !clock.Restore(50,0,double.NaN) && clock.Restore(100,200,10) && clock.TryConsume(10),"invalid cooldown saves are rejected; elapsed offline cooldown is ready");

        ChatSocialAssets.Reload();
        Check(ChatSocialAssets.Templates.templates.Count(t => t.key.StartsWith("company-news.")) == 216,"company news dialogue covers six stances, styles and intensities");
        var fixture = SocialReplay(73129,8,steps:1); var society = fixture.Society;
        var stock = StockManager.Instance.stocks[0]; var bot = society.SocialBots[1];
        bot.Social.Psychology.Companies.Read(stock.symbol).affinity = 80;
        bot.Social.Psychology.Companies.Observe(stock.symbol,701,-1,.32,false,.8,true,10);
        Time.time=10; society.SocialTime.Set(1,550,10);
        var intent = new SocialIntent { roomId=1,motive=SocialMotive.SELF_EVENT,forcedAct=SocialAct.REACT,
            facts=new SocialFacts { symbol=stock.symbol,notice="COMPANY_NEWS_DEFEND",companyNewsId=701,newsObserver=bot.Social.Id,newsHeadline="신규 계약설",companySentiment=-1,eventId=800001 } };
        SocialMessage message=null; society.SocialMessagePublished += item => message=item;
        var generate=typeof(InvestorSociety).GetMethod("GenerateSocialMessage",BindingFlags.Instance|BindingFlags.NonPublic);
        generate.Invoke(society,new object[]{bot,intent});
        Check(message != null && message.templateKey.StartsWith("company-news.") && !message.text.Contains("{"),"actual social pipeline emits a company-specific stance with guarded slots");
        message=null; bot.Social.Psychology.Companies.Observe(stock.symbol,702,-1,.95,true,.8,true,11);
        generate.Invoke(society,new object[]{bot,intent});
        Check(message==null,"superseded article reactions are dropped during typing instead of contradicting newer evidence");
    }
}
```

## 기존 함수 비교

필드 추가: NewsManager는 본문 목록·발행 이벤트, SocialFacts는 기사 ID·원문·관찰자, SocialSnapshot은 기업 기억, PlayerProgression.Snapshot은 현실시간 쿨타임을 담는다. 기존 필드와 저장 버전은 유지한다.

### Assets/Scripts/News/NewsManager.cs · Awake

원본:

```csharp
void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        StockManager.OnStockDelisted += OnDelisted;
        // FakeNewsManager가 없으면 이 오브젝트에 추가 (FakeValueAndNews.md 설계용)
        if (FakeNewsManager.Instance == null)
        {
            gameObject.AddComponent<FakeNewsManager>();
        }
    }
```

수정본:

```csharp
void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        var writing = Resources.Load<TextAsset>("News/news_writing");
        if (writing == null) { throw new System.InvalidOperationException("Missing News/news_writing"); }
        Writing = JsonUtility.FromJson<NewsWritingCatalog>(writing.text);
        if (GetComponent<CompanyNewsAudience>() == null) { gameObject.AddComponent<CompanyNewsAudience>(); }
        StockManager.OnStockDelisted += OnDelisted;
        // FakeNewsManager가 없으면 이 오브젝트에 추가 (FakeValueAndNews.md 설계용)
        if (FakeNewsManager.Instance == null)
        {
            gameObject.AddComponent<FakeNewsManager>();
        }
    }
```

### Assets/Scripts/News/NewsManager.cs · AddNews

원본:

```csharp
public void AddNews(Stock stock, float criticalValue, string title, long newsId = 0)
    {
        if (newsId <= 0)
        {
            newsId = ++nextNewsId;
        }
        else
        {
            nextNewsId = System.Math.Max(nextNewsId, newsId);
        }

        if (stock != null)
        {
            if (!(reservedIds.TryGetValue(stock, out var reservations) && reservations.Remove(newsId)) && publishedIds.TryGetValue(stock, out long lastId) && newsId <= lastId)
            { return; }

            publishedIds[stock] = publishedIds.TryGetValue(stock, out long previousId) ? System.Math.Max(previousId, newsId) : newsId;
            OnMarketNews?.Invoke(stock, newsId, criticalValue);
        }

        string symbol = stock != null ? stock.symbol : "";
        _news.Insert(0, new NewsEntry { symbol = symbol, criticalValue = criticalValue, title = title ?? "" });
        while (_news.Count > MaxNewsCount)
        {
            _news.RemoveAt(_news.Count - 1);
        }

        if (stock != null)
        {
            if (StockInterestManager.Instance != null)
            {
                float interestBonus = Mathf.Abs(criticalValue - 1f) * 1000f;
                StockInterestManager.Instance.AddInterestDelta(stock, interestBonus);
            }

            if (StockNewsAttentionManager.Instance != null)
            {
                StockNewsAttentionManager.Instance.ApplyNewsCriticalValue(stock, criticalValue);
            }
        }

        RefreshUI();
        OnNewsPublished?.Invoke(stock, newsId, title ?? "");
    }
```

수정본:

```csharp
public void AddNews(Stock stock, float criticalValue, string title, long newsId = 0)
    { Publish(stock, criticalValue, title, newsId); }

    public NewsArticle Publish(Stock stock, float criticalValue, string title, long newsId = 0, bool fabricated = false,
        string category = null, int variant = 0, long storyId = 0, bool correction = false)
    {
        if (float.IsNaN(criticalValue) || float.IsInfinity(criticalValue) || criticalValue <= 0 || string.IsNullOrWhiteSpace(title))
        { return null; }
        if (newsId <= 0)
        {
            newsId = ++nextNewsId;
        }
        else
        {
            nextNewsId = System.Math.Max(nextNewsId, newsId);
        }

        if (stock != null)
        {
            if (!(reservedIds.TryGetValue(stock, out var reservations) && reservations.Remove(newsId)) && publishedIds.TryGetValue(stock, out long lastId) && newsId <= lastId)
            { return null; }

            publishedIds[stock] = publishedIds.TryGetValue(stock, out long previousId) ? System.Math.Max(previousId, newsId) : newsId;
            OnMarketNews?.Invoke(stock, newsId, criticalValue);
        }

        string symbol = stock != null ? stock.symbol : "";
        _news.Insert(0, new NewsEntry { symbol = symbol, criticalValue = criticalValue, title = title ?? "" });
        while (_news.Count > MaxNewsCount)
        {
            _news.RemoveAt(_news.Count - 1);
        }

        if (stock != null)
        {
            if (StockInterestManager.Instance != null)
            {
                float interestBonus = Mathf.Abs(criticalValue - 1f) * 1000f;
                StockInterestManager.Instance.AddInterestDelta(stock, interestBonus);
            }

            if (StockNewsAttentionManager.Instance != null)
            {
                StockNewsAttentionManager.Instance.ApplyNewsCriticalValue(stock, criticalValue);
            }
        }

        RefreshUI();
        NewsArticle article = null;
        if (stock != null)
        {
            article = NewsWriting.Compose(Writing, newsId, stock.symbol, stock.stockName, title, (criticalValue - 1) * 8,
                DeviceMarketJournal.Stamp(), Time.time, fabricated, category, variant, storyId, correction);
            Articles.Insert(0, article);
            if (Articles.Count > 40) { Articles.RemoveAt(40); }
            ArticleVersion++;
        }
        OnNewsPublished?.Invoke(stock, newsId, title ?? "");
        if (article != null) { OnArticlePublished?.Invoke(article); }
        return article;
    }
```

### Assets/Scripts/News/NewsManager.cs · Publish

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
public NewsArticle Publish(Stock stock, float criticalValue, string title, long newsId = 0, bool fabricated = false,
        string category = null, int variant = 0, long storyId = 0, bool correction = false)
    {
        if (float.IsNaN(criticalValue) || float.IsInfinity(criticalValue) || criticalValue <= 0 || string.IsNullOrWhiteSpace(title))
        { return null; }
        if (newsId <= 0)
        {
            newsId = ++nextNewsId;
        }
        else
        {
            nextNewsId = System.Math.Max(nextNewsId, newsId);
        }

        if (stock != null)
        {
            if (!(reservedIds.TryGetValue(stock, out var reservations) && reservations.Remove(newsId)) && publishedIds.TryGetValue(stock, out long lastId) && newsId <= lastId)
            { return null; }

            publishedIds[stock] = publishedIds.TryGetValue(stock, out long previousId) ? System.Math.Max(previousId, newsId) : newsId;
            OnMarketNews?.Invoke(stock, newsId, criticalValue);
        }

        string symbol = stock != null ? stock.symbol : "";
        _news.Insert(0, new NewsEntry { symbol = symbol, criticalValue = criticalValue, title = title ?? "" });
        while (_news.Count > MaxNewsCount)
        {
            _news.RemoveAt(_news.Count - 1);
        }

        if (stock != null)
        {
            if (StockInterestManager.Instance != null)
            {
                float interestBonus = Mathf.Abs(criticalValue - 1f) * 1000f;
                StockInterestManager.Instance.AddInterestDelta(stock, interestBonus);
            }

            if (StockNewsAttentionManager.Instance != null)
            {
                StockNewsAttentionManager.Instance.ApplyNewsCriticalValue(stock, criticalValue);
            }
        }

        RefreshUI();
        NewsArticle article = null;
        if (stock != null)
        {
            article = NewsWriting.Compose(Writing, newsId, stock.symbol, stock.stockName, title, (criticalValue - 1) * 8,
                DeviceMarketJournal.Stamp(), Time.time, fabricated, category, variant, storyId, correction);
            Articles.Insert(0, article);
            if (Articles.Count > 40) { Articles.RemoveAt(40); }
            ArticleVersion++;
        }
        OnNewsPublished?.Invoke(stock, newsId, title ?? "");
        if (article != null) { OnArticlePublished?.Invoke(article); }
        return article;
    }
```

### Assets/Scripts/News/FakeNewsManager.cs · Awake

원본:

```csharp
void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
```

수정본:

```csharp
void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }
```

### Assets/Scripts/News/FakeNewsManager.cs · OnEnable

원본:

```csharp
void OnEnable()
    {
        StockManager.OnStockDelisted += OnStockDelisted;
    }
```

수정본:

```csharp
void OnEnable() { StockManager.OnStockDelisted += OnStockDelisted; }
```

### Assets/Scripts/News/FakeNewsManager.cs · OnDisable

원본:

```csharp
void OnDisable()
    {
        StockManager.OnStockDelisted -= OnStockDelisted;
    }
```

수정본:

```csharp
void OnDisable() { StockManager.OnStockDelisted -= OnStockDelisted; }
```

### Assets/Scripts/News/FakeNewsManager.cs · OnDestroy

원본:

```csharp
void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
```

수정본:

```csharp
void OnDestroy() { if (Instance == this) { Instance = null; } }
```

### Assets/Scripts/News/FakeNewsManager.cs · OnStockDelisted

원본:

```csharp
void OnStockDelisted(Stock stock)
    {
        if (stock != null)
            _manipulations.Remove(stock);
    }
```

수정본:

```csharp
void OnStockDelisted(Stock stock)
    {
        if (stock == null) { return; }
        manipulations.Remove(stock); followups.RemoveAll(item => item.stock == stock);
    }
```

### Assets/Scripts/News/FakeNewsManager.cs · RecordManipulation

원본:

```csharp
public void RecordManipulation(Stock stock)
    {
        if (stock == null) return;
        float now = Time.time;
        _manipulations[stock] = (now, false);
    }
```

수정본:

```csharp
public void RecordManipulation(Stock stock)
    {
        if (stock == null) { return; }
        manipulations[stock] = (Time.time, false);
    }
```

### Assets/Scripts/News/FakeNewsManager.cs · CanPublish

원본:

```csharp
public bool CanPublish(Stock stock)
    {
        if (stock == null || !_manipulations.TryGetValue(stock, out var rec))
            return false;
        if (oneNewsPerManipulation && rec.published)
            return false;
        return (Time.time - rec.time) >= minSecondsAfterManipulation;
    }
```

수정본:

```csharp
public bool CanPublish(Stock stock) => stock != null && manipulations.TryGetValue(stock, out var record) &&
        (!oneNewsPerManipulation || !record.published) && Time.time - record.time >= minSecondsAfterManipulation;
```

### Assets/Scripts/News/FakeNewsManager.cs · TryPublishFakeNews

원본:

```csharp
public bool TryPublishFakeNews(Stock stock, bool isPump)
    {
        if (stock == null) return false;
        if (NewsManager.Instance == null) return false;
        if (!_manipulations.TryGetValue(stock, out var rec)) return false;

        float elapsed = Time.time - rec.time;
        if (elapsed < minSecondsAfterManipulation) return false;
        if (oneNewsPerManipulation && rec.published) return false;

        string title = GetFakeNewsTitle(stock, isPump);
        if (string.IsNullOrEmpty(title)) return false;

        float criticalValue = isPump ? 1.08f : 0.92f; // 호재/악재 색상용
        NewsManager.Instance.AddNews(stock, criticalValue, title);

        _manipulations[stock] = (rec.time, true);
        Debug.Log($"[세력] 가짜 뉴스 발행 — {stock.stockName}({stock.symbol}) {(isPump ? "호재" : "악재")} | {title}");
        return true;
    }
```

수정본:

```csharp
public bool TryPublishFakeNews(Stock stock, bool isPump)
    {
        if (!CanPublish(stock) || NewsManager.Instance == null || !stock.AcceptsOrders) { return false; }
        var article = PublishRumor(stock, "contracts", isPump, Mathf.FloorToInt(Time.time));
        if (article == null) { return false; }
        manipulations[stock] = (manipulations[stock].time, true); return true;
    }
```

### Assets/Scripts/News/FakeNewsManager.cs · PreviewPlayer

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
public NewsArticle PreviewPlayer(Stock stock, string category, bool positive, int variant)
    {
        var manager = NewsManager.Instance;
        if (stock == null || manager == null) { return null; }
        string title = NewsWriting.RumorTitle(manager.Writing, stock.symbol, stock.stockName, category, positive, variant);
        return NewsWriting.Compose(manager.Writing, 0, stock.symbol, stock.stockName, title, positive ? .64 : -.64,
            DeviceMarketJournal.Stamp(), Time.time, true, category, variant);
    }
```

### Assets/Scripts/News/FakeNewsManager.cs · TryPublishPlayer

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
public bool TryPublishPlayer(Stock stock, string category, bool positive, int variant, out NewsArticle article, out string reason)
    {
        article = null;
        if (!PlayerUnlocked) { reason = "2번 계좌 해금 후 사용할 수 있습니다."; return false; }
        if (stock == null || !stock.IsListed || !stock.AcceptsOrders || !MarketDayController.CanTrade)
        { reason = "거래 중인 종목을 선택하세요."; return false; }
        if (PlayerCooldown > 0) { reason = "다음 발행까지 " + FormatCooldown(PlayerCooldown); return false; }
        if (NewsManager.Instance == null) { reason = "뉴스 연결을 준비하고 있습니다."; return false; }
        article = PublishRumor(stock, category, positive, variant);
        if (article == null) { reason = "기사 발행에 실패했습니다. 다시 확인하세요."; return false; }
        playerClock.TryConsume(Time.realtimeSinceStartupAsDouble);
        reason = "발행 완료 · 다음 발행까지 10:00";
        ChatSocialAudit.Journal.Event(new SocialEvent { id = article.id, time = ChatSocialAudit.GameMinute, actor = -1,
            kind = "PLAYER_NEWS_PUBLISHED", reason = stock.symbol + " " + category, value = article.sentiment });
        return true;
    }
```

### Assets/Scripts/News/FakeNewsManager.cs · PublishRumor

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
private NewsArticle PublishRumor(Stock stock, string category, bool positive, int variant)
    {
        var manager = NewsManager.Instance;
        string title = NewsWriting.RumorTitle(manager.Writing, stock.symbol, stock.stockName, category, positive, variant);
        var article = manager.Publish(stock, positive ? 1.08f : .92f, title, fabricated: true, category: category, variant: variant);
        if (article != null && NewsWriting.Hash(article.id + ":followup:" + stock.symbol) % 10000 < followupChance * 10000)
        {
            if (followups.Count >= 16) { followups.RemoveAt(0); }
            followups.Add((stock, article, Time.time + followupDelaySeconds + NewsWriting.Hash(article.id.ToString()) % 45));
        }
        return article;
    }
```

### Assets/Scripts/News/FakeNewsManager.cs · Update

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
void Update()
    {
        if (Time.time < nextCheck || !MarketDayController.CanTrade || NewsManager.Instance == null) { return; }
        nextCheck = Time.time + 1;
        for (int i = followups.Count - 1; i >= 0; i--)
        {
            var item = followups[i];
            if (Time.time < item.due) { continue; }
            followups.RemoveAt(i);
            if (item.stock == null || item.stock.IsDelisted) { continue; }
            NewsManager.Instance.Publish(item.stock, item.article.sentiment > 0 ? .94f : 1.06f,
                item.stock.stockName + " 관련 " + item.article.categoryLabel + " 주장, 확인 자료 없는 것으로 드러나",
                category: item.article.category, storyId: item.article.storyId, correction: true);
        }
    }
```

### Assets/Scripts/News/FakeNewsManager.cs · RestorePlayerCooldown

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
public bool RestorePlayerCooldown(double remaining, long savedUtcTicks)
    {
        if (savedUtcTicks < 0 || savedUtcTicks > DateTime.MaxValue.Ticks) { return false; }
        double offline = savedUtcTicks == 0 ? 0 : Math.Max(0, (DateTime.UtcNow.Ticks - savedUtcTicks) / (double)TimeSpan.TicksPerSecond);
        return playerClock.Restore(remaining, offline, Time.realtimeSinceStartupAsDouble);
    }
```

### Assets/Scripts/News/FakeNewsManager.cs · FormatCooldown

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
public static string FormatCooldown(double value)
    {
        int seconds = (int)Math.Ceiling(Math.Max(0, value));
        return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }
```

### Assets/Scripts/NightShift/InvestorPsychology.cs · InvestorPsychology

원본:

```csharp
public InvestorPsychology(int id, int seed)
    {
        var random = new System.Random(unchecked(seed + id * 7919));
        Greed = BaselineGreed = 50f + (float)random.NextDouble() * 30f;
        Credulity = .15f + (float)random.NextDouble() * .7f;
        Activity = .15f + (float)random.NextDouble() * .8f;
        ActivityOffset = (float)random.NextDouble() * 180f;
        InitializeSocial(id, seed);
    }
```

수정본:

```csharp
public InvestorPsychology(int id, int seed)
    {
        Companies = new CompanyOpinionBook(seed, id);
        var random = new System.Random(unchecked(seed + id * 7919));
        Greed = BaselineGreed = 50f + (float)random.NextDouble() * 30f;
        Credulity = .15f + (float)random.NextDouble() * .7f;
        Activity = .15f + (float)random.NextDouble() * .8f;
        ActivityOffset = (float)random.NextDouble() * 180f;
        InitializeSocial(id, seed);
    }
```

### Assets/Scripts/NightShift/ChatSocialData.cs · CaptureSocial

원본:

```csharp
public SocialSnapshot CaptureSocial()
    {
        var relations = new List<SocialRelation>(); foreach (var row in Relations.Values) { relations.Add(CopyRelation(row)); }
        relations.Sort((a, b) => a.to.CompareTo(b.to));
        var memories = new List<SocialMemory>(); foreach (var row in Memories) { memories.Add(CopyMemory(row)); }
        var lots = new List<FomoLotSnapshot>();
        foreach (var row in FomoLots.Values) { lots.Add(new FomoLotSnapshot { symbol = row.symbol, quantity = row.quantity.ToString(CultureInfo.InvariantCulture), cost = row.cost.ToString(CultureInfo.InvariantCulture), realized = row.realized.ToString(CultureInfo.InvariantCulture) }); }
        lots.Sort((a, b) => string.CompareOrdinal(a.symbol, b.symbol));
        return new SocialSnapshot { id = SocialId, room = Room, greed = Greed, fear = Fear, trust = Trust, betrayal = Betrayal,
            fomo = 0, fomoSymbol = FomoSymbol, fomoUntil = FomoUntil, fomoCooldownUntil = FomoCooldownUntil, quadrant = Quadrant, randomState = SocialRng.State,
            traits = Traits, relations = relations.ToArray(), memories = memories.ToArray(), fomoLots = lots.ToArray(), rejoin = SaveCooldowns(RejoinAt), mutes = SaveCooldowns(MutedUntil),
            knownPlayerRooms = SaveCooldowns(KnownPlayerRooms), invitedPlayerRooms = InvitedPlayerRooms,
            promisedAction = PromisedAction, promisedSymbol = PromisedSymbol, promiseMessage = PromiseMessage,
            lastQuadrantReview = LastQuadrantReview, lastFomoObservation = LastFomoObservation, lastRelationshipSpeech = LastRelationshipSpeech };
    }
```

수정본:

```csharp
public SocialSnapshot CaptureSocial()
    {
        var relations = new List<SocialRelation>(); foreach (var row in Relations.Values) { relations.Add(CopyRelation(row)); }
        relations.Sort((a, b) => a.to.CompareTo(b.to));
        var memories = new List<SocialMemory>(); foreach (var row in Memories) { memories.Add(CopyMemory(row)); }
        var lots = new List<FomoLotSnapshot>();
        foreach (var row in FomoLots.Values) { lots.Add(new FomoLotSnapshot { symbol = row.symbol, quantity = row.quantity.ToString(CultureInfo.InvariantCulture), cost = row.cost.ToString(CultureInfo.InvariantCulture), realized = row.realized.ToString(CultureInfo.InvariantCulture) }); }
        lots.Sort((a, b) => string.CompareOrdinal(a.symbol, b.symbol));
        return new SocialSnapshot { id = SocialId, room = Room, greed = Greed, fear = Fear, trust = Trust, betrayal = Betrayal,
            fomo = 0, fomoSymbol = FomoSymbol, fomoUntil = FomoUntil, fomoCooldownUntil = FomoCooldownUntil, quadrant = Quadrant, randomState = SocialRng.State,
            traits = Traits, relations = relations.ToArray(), memories = memories.ToArray(), fomoLots = lots.ToArray(), rejoin = SaveCooldowns(RejoinAt), mutes = SaveCooldowns(MutedUntil),
            knownPlayerRooms = SaveCooldowns(KnownPlayerRooms), invitedPlayerRooms = InvitedPlayerRooms, companies = Companies.Capture(),
            promisedAction = PromisedAction, promisedSymbol = PromisedSymbol, promiseMessage = PromiseMessage,
            lastQuadrantReview = LastQuadrantReview, lastFomoObservation = LastFomoObservation, lastRelationshipSpeech = LastRelationshipSpeech };
    }
```

### Assets/Scripts/NightShift/ChatSocialData.cs · RestoreSocial

원본:

```csharp
public bool RestoreSocial(SocialSnapshot saved)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.id != SocialId ||
            !Finite(saved.trust) || !Finite(saved.betrayal) || !Finite(saved.greed) || !Finite(saved.fear) || !Finite(saved.fomoUntil) || !Finite(saved.fomoCooldownUntil) ||
            !Finite(saved.lastQuadrantReview) || !Finite(saved.lastFomoObservation) || !Finite(saved.lastRelationshipSpeech) || saved.traits != null && !saved.traits.Valid)
        { return false; }
        var lots = new Dictionary<string, FomoLot>();
        if (saved.fomoLots != null)
        {
            foreach (var row in saved.fomoLots)
            {
                if (row == null || string.IsNullOrEmpty(row.symbol) || lots.ContainsKey(row.symbol) ||
                    !decimal.TryParse(row.quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal quantity) || quantity < 0 ||
                    !decimal.TryParse(row.cost, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal cost) || cost < 0 ||
                    !decimal.TryParse(row.realized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal realized)) { return false; }
                lots[row.symbol] = new FomoLot { symbol = row.symbol, quantity = quantity, cost = cost, realized = realized };
            }
        }
        if (saved.relations != null) { foreach (var row in saved.relations) { if (row != null && (!Finite(row.anger) || !Finite(row.affinity) || !Finite(row.lastInteractionAt) || !Finite(row.lastWarningAt))) { return false; } } }
        if (saved.memories != null) { foreach (var row in saved.memories) { if (row != null && (!Finite(row.at) || row.usedCount < 0)) { return false; } } }
        foreach (var group in new[] { saved.rejoin, saved.mutes }) { if (group != null) { foreach (var row in group) { if (row == null || row.room < 1 || row.room > InvestorSociety.MaximumRoomId || !Finite(row.until)) { return false; } } } }
        if (saved.invitedPlayerRooms < 0 || saved.invitedPlayerRooms >= 1 << InvestorSociety.PlayerRoomCount) { return false; }
        if (saved.knownPlayerRooms != null) { foreach (var row in saved.knownPlayerRooms) { if (row == null || row.room < 1 || row.room > InvestorSociety.PlayerRoomCount || !Finite(row.until)) { return false; } } }
        if (!Enum.IsDefined(typeof(SocialPromisedAction), saved.promisedAction) || saved.promisedAction != SocialPromisedAction.None && string.IsNullOrEmpty(saved.promisedSymbol)) { return false; }
        Greed = saved.greed; Fear = saved.fear; Trust = saved.trust; Betrayal = saved.betrayal;
        PromisedAction = saved.promisedAction; PromisedSymbol = saved.promisedSymbol; PromiseMessage = saved.promiseMessage;
        Quadrant = saved.quadrant; FomoSymbol = saved.version >= 2 ? saved.fomoSymbol : null;
        Room = Math.Max(0, Math.Min(InvestorSociety.MaximumRoomId, saved.room));
        if (Room > InvestorSociety.AvailablePlayerRoomCount && Room <= InvestorSociety.PlayerRoomCount) { Room = 0; }
        FomoUntil = saved.version >= 2 ? Math.Max(0, saved.fomoUntil) : 0;
        FomoCooldownUntil = saved.version >= 2 ? Math.Max(0, saved.fomoCooldownUntil) : 0;
        LastQuadrantReview = saved.lastQuadrantReview; LastFomoObservation = saved.lastFomoObservation; LastRelationshipSpeech = saved.lastRelationshipSpeech;
        FomoLots.Clear(); foreach (var row in lots) { FomoLots[row.Key] = row.Value; }
        RejoinAt.Clear(); MutedUntil.Clear();
        KnownPlayerRooms.Clear(); InvitedPlayerRooms = saved.invitedPlayerRooms;
        if (saved.knownPlayerRooms != null) { foreach (var row in saved.knownPlayerRooms) { KnownPlayerRooms[row.room] = row.until; } }
        if (saved.rejoin != null) { foreach (var row in saved.rejoin) { RejoinAt[row.room] = row.until; } }
        if (saved.mutes != null) { foreach (var row in saved.mutes) { MutedUntil[row.room] = row.until; } }
        if (saved.traits != null) { saved.traits.MigrateSpeech(Traits); Traits = saved.traits; }
        if (saved.randomState != 0) { SocialRng.Restore(saved.randomState); }
        Relations.Clear(); Memories.Clear();
        if (saved.relations != null)
        {
            foreach (var row in saved.relations)
            {
                if (row == null || row.to == SocialId) { continue; }
                Relations[row.to] = CopyRelation(row);
            }
        }
        if (saved.memories != null) { foreach (var memory in saved.memories) { if (memory != null) { Remember(CopyMemory(memory)); } } }
        Clamp(); AuditEmotion("restore"); return true;
    }
```

수정본:

```csharp
public bool RestoreSocial(SocialSnapshot saved)
    {
        if (saved == null || saved.version < 1 || saved.version > 2 || saved.id != SocialId ||
            !Finite(saved.trust) || !Finite(saved.betrayal) || !Finite(saved.greed) || !Finite(saved.fear) || !Finite(saved.fomoUntil) || !Finite(saved.fomoCooldownUntil) ||
            !Finite(saved.lastQuadrantReview) || !Finite(saved.lastFomoObservation) || !Finite(saved.lastRelationshipSpeech) || saved.traits != null && !saved.traits.Valid)
        { return false; }
        var lots = new Dictionary<string, FomoLot>();
        if (saved.fomoLots != null)
        {
            foreach (var row in saved.fomoLots)
            {
                if (row == null || string.IsNullOrEmpty(row.symbol) || lots.ContainsKey(row.symbol) ||
                    !decimal.TryParse(row.quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal quantity) || quantity < 0 ||
                    !decimal.TryParse(row.cost, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal cost) || cost < 0 ||
                    !decimal.TryParse(row.realized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal realized)) { return false; }
                lots[row.symbol] = new FomoLot { symbol = row.symbol, quantity = quantity, cost = cost, realized = realized };
            }
        }
        if (saved.relations != null) { foreach (var row in saved.relations) { if (row != null && (!Finite(row.anger) || !Finite(row.affinity) || !Finite(row.lastInteractionAt) || !Finite(row.lastWarningAt))) { return false; } } }
        if (saved.memories != null) { foreach (var row in saved.memories) { if (row != null && (!Finite(row.at) || row.usedCount < 0)) { return false; } } }
        foreach (var group in new[] { saved.rejoin, saved.mutes }) { if (group != null) { foreach (var row in group) { if (row == null || row.room < 1 || row.room > InvestorSociety.MaximumRoomId || !Finite(row.until)) { return false; } } } }
        if (saved.invitedPlayerRooms < 0 || saved.invitedPlayerRooms >= 1 << InvestorSociety.PlayerRoomCount) { return false; }
        if (saved.knownPlayerRooms != null) { foreach (var row in saved.knownPlayerRooms) { if (row == null || row.room < 1 || row.room > InvestorSociety.PlayerRoomCount || !Finite(row.until)) { return false; } } }
        if (!CompanyOpinionBook.Valid(saved.companies)) { return false; }
        if (!Enum.IsDefined(typeof(SocialPromisedAction), saved.promisedAction) || saved.promisedAction != SocialPromisedAction.None && string.IsNullOrEmpty(saved.promisedSymbol)) { return false; }
        Greed = saved.greed; Fear = saved.fear; Trust = saved.trust; Betrayal = saved.betrayal;
        PromisedAction = saved.promisedAction; PromisedSymbol = saved.promisedSymbol; PromiseMessage = saved.promiseMessage;
        Quadrant = saved.quadrant; FomoSymbol = saved.version >= 2 ? saved.fomoSymbol : null;
        Room = Math.Max(0, Math.Min(InvestorSociety.MaximumRoomId, saved.room));
        if (Room > InvestorSociety.AvailablePlayerRoomCount && Room <= InvestorSociety.PlayerRoomCount) { Room = 0; }
        FomoUntil = saved.version >= 2 ? Math.Max(0, saved.fomoUntil) : 0;
        FomoCooldownUntil = saved.version >= 2 ? Math.Max(0, saved.fomoCooldownUntil) : 0;
        LastQuadrantReview = saved.lastQuadrantReview; LastFomoObservation = saved.lastFomoObservation; LastRelationshipSpeech = saved.lastRelationshipSpeech;
        FomoLots.Clear(); foreach (var row in lots) { FomoLots[row.Key] = row.Value; }
        RejoinAt.Clear(); MutedUntil.Clear();
        KnownPlayerRooms.Clear(); InvitedPlayerRooms = saved.invitedPlayerRooms;
        Companies.Restore(saved.companies);
        if (saved.knownPlayerRooms != null) { foreach (var row in saved.knownPlayerRooms) { KnownPlayerRooms[row.room] = row.until; } }
        if (saved.rejoin != null) { foreach (var row in saved.rejoin) { RejoinAt[row.room] = row.until; } }
        if (saved.mutes != null) { foreach (var row in saved.mutes) { MutedUntil[row.room] = row.until; } }
        if (saved.traits != null) { saved.traits.MigrateSpeech(Traits); Traits = saved.traits; }
        if (saved.randomState != 0) { SocialRng.Restore(saved.randomState); }
        Relations.Clear(); Memories.Clear();
        if (saved.relations != null)
        {
            foreach (var row in saved.relations)
            {
                if (row == null || row.to == SocialId) { continue; }
                Relations[row.to] = CopyRelation(row);
            }
        }
        if (saved.memories != null) { foreach (var memory in saved.memories) { if (memory != null) { Remember(CopyMemory(memory)); } } }
        Clamp(); AuditEmotion("restore"); return true;
    }
```

### Assets/Scripts/NightShift/ChatSocialPipeline.cs · GenerateSocialMessage

원본:

```csharp
private void GenerateSocialMessage(TradeBot bot, SocialIntent intent)
    {
        var mind = bot.Social.Psychology; var room = SocialRoom(intent.roomId);
        if (!Present(bot, intent.roomId) || !ValidateSocialIntent(bot, intent))
        { return; }
        // Re-read live portfolio facts when an intention is finally processed, not when it was queued.
        Stock topic = FindSocialStock(intent.facts?.symbol);
        if (topic == null && LiveConversationEnabled)
        { topic = ChooseRoomTopic(bot, intent.roomId, mind.SocialRng.Next()); }
        var facts = CaptureFacts(bot, topic);
        facts.sourceEvent = intent.facts?.sourceEvent;
        facts.notice = intent.facts?.notice; facts.departedName = intent.facts?.departedName;
        PreserveWatchObservation(bot, intent, facts);
        if (!RefreshMarketRiskFacts(bot, intent, facts)) { return; }
        if (facts.notice != null) { facts.eventId = intent.facts.eventId; }
        if (intent.facts != null && intent.facts.directLead) { facts = intent.facts; }
        if (intent.facts != null && intent.facts.signal && RefreshOfferedFacts != null)
        { facts = RefreshOfferedFacts(bot, intent.facts) ?? facts; }
        PrepareSocialFacts(bot, intent, ref facts);
        var candidates = BuildSocialWeights(bot, intent, facts);
        SocialAttempts++;
        var debugWeights = candidates.ToArray();
        while (candidates.Count > 0)
        {
            double total = 0; foreach (var candidate in candidates) { total += candidate.value; }
            if (total <= 0)
            { return; }
            double roll = mind.SocialRng.Next() * total; int chosen = candidates.Count - 1;
            for (int i = 0; i < candidates.Count; i++) { roll -= candidates[i].value; if (roll <= 0) { chosen = i; break; } }
            var selection = candidates[chosen]; candidates.RemoveAt(chosen);
            if (!Enum.TryParse<SocialAct>(selection.key, out var act))
            { return; }
            if (act == SocialAct.PRAISE_PLAYER || act == SocialAct.WARN_PLAYER || act == SocialAct.CALL_PLAYER) { intent.targetId = -1; }
            else if (intent.threadId == 0 && intent.replyToId == 0 && (act == SocialAct.LAMENT || act == SocialAct.REACT)) { intent.targetId = 0; }
            else if (intent.targetId == -1 && mind.Quadrant == InvestorQuadrant.Indifferent && intent.replyToId == 0) { intent.targetId = 0; }
            var templates = EligibleTemplates(bot, intent, facts, act);
            if (templates.Count == 0)
            { RecordTemplateMiss(act); continue; }
            var template = templates[mind.SocialRng.Range(0, templates.Count)];
            var message = new SocialMessage { id = ChatSocialIds.Next(), roomId = intent.roomId, senderId = mind.SocialId, targetId = intent.targetId,
                at = SocialTime.GameMinute, realAt = SocialTime.Seconds, act = act, replyToId = intent.replyToId, threadId = intent.threadId,
                facts = facts, topicEvent = intent.motive.ToString(), templateKey = template.key, isLie = false, memory = template.variant == "memory",
                promisedAction = template.promisedAction,
                debug = new SocialMessageDebug { trigger = intent.motive.ToString(), causeId = intent.causeId, weights = debugWeights, slots = facts } };
            string target = message.targetId == -1 ? "방장" : SocialBots.TryGetValue(message.targetId, out var other) ? other.Social.Nickname : "";
            socialMessageIndex.TryGetValue(message.replyToId, out var parentMessage);
            message.text = RenderSocialText(mind, template, message, parentMessage, target, room.recent);
            if (message.targetId != 0) { message.text = "@" + target + " " + message.text; }
            BeforeSocialPublish(bot, intent, message);
            if (message.promisedAction != SocialPromisedAction.None) { mind.PromiseTrade(message.promisedAction, facts.symbol, message.id); }
            mind.SocialLastSpeech = SocialTime.Seconds; bot.Social.LastSpeech = (float)SocialTime.Seconds;
            mind.RecentTemplateKeys.Enqueue(template.key);
            while (mind.RecentTemplateKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentTemplateKeys.Dequeue(); }
            mind.RecentSpeechKeys.Enqueue(message.debug.speechKey ?? "");
            while (mind.RecentSpeechKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentSpeechKeys.Dequeue(); }
            room.recent.Enqueue(message); while (room.recent.Count > Config.Int("roomRecentTemplates")) { room.recent.Dequeue(); }
            room.sent.Enqueue(SocialTime.Seconds);
            consecutiveAct[mind.SocialId] = lastAct.TryGetValue(mind.SocialId, out var previous) && previous == act ? consecutiveAct[mind.SocialId] + 1 : 1;
            lastAct[mind.SocialId] = act;
            socialMessageIndex[message.id] = message; socialMessageOrder.Enqueue(message.id);
            while (socialMessageOrder.Count > Config.Int("logCapacity")) { socialMessageIndex.Remove(socialMessageOrder.Dequeue()); }
            ObserveConversationTopic(message);
            SocialPublished++; SocialMessagePublished?.Invoke(message); AfterSocialPublish(bot, intent, message);
            return;
        }
        TemplateBlocked++;
    }
```

수정본:

```csharp
private void GenerateSocialMessage(TradeBot bot, SocialIntent intent)
    {
        var mind = bot.Social.Psychology; var room = SocialRoom(intent.roomId);
        if (!Present(bot, intent.roomId) || !ValidateSocialIntent(bot, intent))
        { return; }
        // Re-read live portfolio facts when an intention is finally processed, not when it was queued.
        Stock topic = FindSocialStock(intent.facts?.symbol);
        if (topic == null && LiveConversationEnabled)
        { topic = ChooseRoomTopic(bot, intent.roomId, mind.SocialRng.Next()); }
        var facts = CaptureFacts(bot, topic);
        facts.sourceEvent = intent.facts?.sourceEvent;
        facts.notice = intent.facts?.notice; facts.departedName = intent.facts?.departedName;
        PreserveWatchObservation(bot, intent, facts);
        if (!RefreshMarketRiskFacts(bot, intent, facts)) { return; }
        if (!PreserveCompanyNews(bot, intent, facts)) { return; }
        if (facts.notice != null) { facts.eventId = intent.facts.eventId; }
        if (intent.facts != null && intent.facts.directLead) { facts = intent.facts; }
        if (intent.facts != null && intent.facts.signal && RefreshOfferedFacts != null)
        { facts = RefreshOfferedFacts(bot, intent.facts) ?? facts; }
        PrepareSocialFacts(bot, intent, ref facts);
        var candidates = BuildSocialWeights(bot, intent, facts);
        SocialAttempts++;
        var debugWeights = candidates.ToArray();
        while (candidates.Count > 0)
        {
            double total = 0; foreach (var candidate in candidates) { total += candidate.value; }
            if (total <= 0)
            { return; }
            double roll = mind.SocialRng.Next() * total; int chosen = candidates.Count - 1;
            for (int i = 0; i < candidates.Count; i++) { roll -= candidates[i].value; if (roll <= 0) { chosen = i; break; } }
            var selection = candidates[chosen]; candidates.RemoveAt(chosen);
            if (!Enum.TryParse<SocialAct>(selection.key, out var act))
            { return; }
            if (act == SocialAct.PRAISE_PLAYER || act == SocialAct.WARN_PLAYER || act == SocialAct.CALL_PLAYER) { intent.targetId = -1; }
            else if (intent.threadId == 0 && intent.replyToId == 0 && (act == SocialAct.LAMENT || act == SocialAct.REACT)) { intent.targetId = 0; }
            else if (intent.targetId == -1 && mind.Quadrant == InvestorQuadrant.Indifferent && intent.replyToId == 0) { intent.targetId = 0; }
            var templates = EligibleTemplates(bot, intent, facts, act);
            if (templates.Count == 0)
            { RecordTemplateMiss(act); continue; }
            var template = templates[mind.SocialRng.Range(0, templates.Count)];
            var message = new SocialMessage { id = ChatSocialIds.Next(), roomId = intent.roomId, senderId = mind.SocialId, targetId = intent.targetId,
                at = SocialTime.GameMinute, realAt = SocialTime.Seconds, act = act, replyToId = intent.replyToId, threadId = intent.threadId,
                facts = facts, topicEvent = intent.motive.ToString(), templateKey = template.key, isLie = false, memory = template.variant == "memory",
                promisedAction = template.promisedAction,
                debug = new SocialMessageDebug { trigger = intent.motive.ToString(), causeId = intent.causeId, weights = debugWeights, slots = facts } };
            string target = message.targetId == -1 ? "방장" : SocialBots.TryGetValue(message.targetId, out var other) ? other.Social.Nickname : "";
            socialMessageIndex.TryGetValue(message.replyToId, out var parentMessage);
            message.text = RenderSocialText(mind, template, message, parentMessage, target, room.recent);
            if (message.targetId != 0) { message.text = "@" + target + " " + message.text; }
            BeforeSocialPublish(bot, intent, message);
            if (message.promisedAction != SocialPromisedAction.None) { mind.PromiseTrade(message.promisedAction, facts.symbol, message.id); }
            mind.SocialLastSpeech = SocialTime.Seconds; bot.Social.LastSpeech = (float)SocialTime.Seconds;
            mind.RecentTemplateKeys.Enqueue(template.key);
            while (mind.RecentTemplateKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentTemplateKeys.Dequeue(); }
            mind.RecentSpeechKeys.Enqueue(message.debug.speechKey ?? "");
            while (mind.RecentSpeechKeys.Count > Config.Int("botRecentTemplates")) { mind.RecentSpeechKeys.Dequeue(); }
            room.recent.Enqueue(message); while (room.recent.Count > Config.Int("roomRecentTemplates")) { room.recent.Dequeue(); }
            room.sent.Enqueue(SocialTime.Seconds);
            consecutiveAct[mind.SocialId] = lastAct.TryGetValue(mind.SocialId, out var previous) && previous == act ? consecutiveAct[mind.SocialId] + 1 : 1;
            lastAct[mind.SocialId] = act;
            socialMessageIndex[message.id] = message; socialMessageOrder.Enqueue(message.id);
            while (socialMessageOrder.Count > Config.Int("logCapacity")) { socialMessageIndex.Remove(socialMessageOrder.Dequeue()); }
            ObserveConversationTopic(message);
            SocialPublished++; SocialMessagePublished?.Invoke(message); AfterSocialPublish(bot, intent, message);
            return;
        }
        TemplateBlocked++;
    }
```

### Assets/Scripts/NightShift/ChatSocialPipeline.cs · Slot

원본:

```csharp
private static string Slot(SocialFacts f, string slot, string target) => slot switch
    {
        "symbol" or "stockName" => f.stockName ?? f.symbol, "target" => target, "pct" => f.pct, "amount" => f.amount,
        "avgPrice" => f.avgPrice, "price" => f.price, "quantity" => f.quantity, "profitKind" => f.profitKind, "memSymbol" => f.memSymbol, "memPct" => f.memPct, "memWhen" => f.memWhen,
        "bidStatus" => f.bidStatus, "askStatus" => f.askStatus, "signalResult" => f.signalResult, "departedName" => f.departedName, "memAmount" => f.memAmount,
        "seenPrice" => f.observationPrice, "movePct" => f.observationPercent,
        "riskLabel" => IsMarketRiskNotice(f.notice) ? MarketRiskLabel(f.riskGrade) : null,
        "seenMinutes" => f.observationPrice != null ? (f.observedAt - f.observationAt).ToString("0.#", SocialCulture) : null, _ => null
    };
```

수정본:

```csharp
private static string Slot(SocialFacts f, string slot, string target) => slot switch
    {
        "symbol" or "stockName" => f.stockName ?? f.symbol, "target" => target, "pct" => f.pct, "amount" => f.amount,
        "avgPrice" => f.avgPrice, "price" => f.price, "quantity" => f.quantity, "profitKind" => f.profitKind, "memSymbol" => f.memSymbol, "memPct" => f.memPct, "memWhen" => f.memWhen,
        "bidStatus" => f.bidStatus, "askStatus" => f.askStatus, "signalResult" => f.signalResult, "departedName" => f.departedName, "memAmount" => f.memAmount,
        "seenPrice" => f.observationPrice, "movePct" => f.observationPercent,
        "riskLabel" => IsMarketRiskNotice(f.notice) ? MarketRiskLabel(f.riskGrade) : null,
        "newsHeadline" => f.newsHeadline,
        "seenMinutes" => f.observationPrice != null ? (f.observedAt - f.observationAt).ToString("0.#", SocialCulture) : null, _ => null
    };
```

### Assets/Scripts/NightShift/ChatSocialEmotions.cs · ApplySocialEffectsCore

원본:

```csharp
private void ApplySocialEffectsCore(TradeBot bot, SocialMessage message, List<TradeBot> witnesses, bool direct)
    {
        if (direct && message.targetId > 0 && SocialBots.TryGetValue(message.targetId, out var receiver))
        {
            double anger = Effect(message.act, "anger");
            if (message.memory && (message.act == SocialAct.MOCK || message.act == SocialAct.REBUT)) { anger *= Config.Get("memoryAngerScale"); }
            ChangeRelation(receiver, message.senderId, "anger", anger, "direct-message", message.id, message.roomId, threadId: message.threadId);
            ChangeRelation(receiver, message.senderId, "affinity", Effect(message.act, "affinity"), "direct-message", message.id, message.roomId);
            double fear = Effect(message.act, "fear");
            if (message.act != SocialAct.MOCK || CaptureFacts(receiver, FindSocialStock(message.facts?.symbol)).losing)
            { receiver.Social.Psychology.SocialDelta("fear", fear, "direct-message", message.id); }
        }
        var thread = FindSocialThread(message.threadId);
        if (direct && message.act == SocialAct.MEDIATE && thread != null) { Mediate(bot, message, thread); }
        if (thread != null && thread.kind == SocialThreadKind.ARGUMENT)
        {
            if (!fightWitnesses.TryGetValue(thread.id, out var seen)) { seen = new HashSet<int>(); fightWitnesses[thread.id] = seen; }
            foreach (var witness in witnesses) { seen.Add(witness.Social.Id); }
        }
        foreach (var witness in witnesses)
        {
            if (witness.Social.Id == message.targetId)
            { continue; }
            var mind = witness.Social.Psychology;
            ObserveRoomReferral(witness, bot, message);
            RememberWitnessedResult(witness, message);
            if (mind.AngerStage(message.senderId) >= 2 && mind.ActiveThread == 0 && !pendingReplies.ContainsKey(mind.SocialId) && !pendingOrigins.ContainsKey(mind.SocialId))
            {
                if (thread != null && thread.Active && thread.participants.Count < Config.Int("maxParticipants") && !thread.participants.Contains(mind.SocialId))
                {
                    mind.Intentions.Add(new SocialIntent { roomId = message.roomId, motive = SocialMotive.WITNESSED_EVENT, forcedAct = SocialAct.REBUT,
                        threadId = thread.id, targetId = message.senderId, facts = message.facts, due = SocialTime.Seconds + Config.Get("replyDelayMin"), expires = SocialTime.Seconds + Config.Get("witnessExpiry") });
                    thread.participants.Add(mind.SocialId); mind.ActiveThread = thread.id;
                }
                else if (thread == null && CanStartWith(witness, bot, message.roomId))
                { Offer(witness, message.roomId, SocialMotive.WITNESSED_EVENT, message.facts, SocialAct.REBUT, message.senderId, message.id); }
            }
            double multiplier = Config.Get("witnessHalfSuggestibility") + mind.Traits.Suggestibility / 100d;
            if (message.targetId > 0 && mind.Relation(message.targetId).affinity >= Config.Get("friendThreshold"))
            {
                double anger = message.act == SocialAct.MOCK ? Config.Get("friendMockAnger") : message.act == SocialAct.INSULT ? Config.Get("friendInsultAnger") : 0;
                WitnessRelation(witness, message, "anger", anger * multiplier);
            }
            if (message.act == SocialAct.INSULT) { WitnessRelation(witness, message, "affinity", Config.Get("insultWitnessAffinity") * multiplier); }
            if (message.act == SocialAct.LAMENT)
            {
                bool held = CaptureFacts(witness, FindSocialStock(message.facts?.symbol)).held;
                WitnessEmotion(witness, message, "fear", (held ? Config.Get("lamentHeldFear") : Config.Get("lamentOtherFear")) * multiplier);
            }
            if (message.act == SocialAct.BRAG)
            {
                var own = CaptureFacts(witness, FindSocialStock(message.facts?.symbol));
                decimal.TryParse(own.pct, System.Globalization.NumberStyles.Number, SocialCulture, out decimal ownReturn);
                decimal.TryParse(message.facts?.pct, System.Globalization.NumberStyles.Number, SocialCulture, out decimal authorReturn);
                if (authorReturn > ownReturn)
                {
                    WitnessEmotion(witness, message, "greed", Config.Get("bragGreed") * multiplier);
                    ConsiderSocialFomo(witness, message);
                }
                if (own.losing) { WitnessRelation(witness, message, "affinity", Config.Get("losingBragAffinity") * multiplier); }
            }
            if ((message.act == SocialAct.PRAISE_PLAYER || message.act == SocialAct.WARN_PLAYER) && message.facts.eventId > 0 && mind.SocialHeardEvents.Add(message.facts.eventId))
            { WitnessEmotion(witness, message, "trust", (message.act == SocialAct.PRAISE_PLAYER ? Config.Get("praiseTrust") : Config.Get("warnTrust")) * multiplier); }
        }
        if (direct && message.act == SocialAct.FAREWELL && bot.Social.Psychology.ExitAt >= 0 && SocialTime.Seconds >= bot.Social.Psychology.ExitAt)
        { DepartSocial(bot, message, witnesses); }
        ApplyModerationReaction(bot, message, witnesses);
    }
```

수정본:

```csharp
private void ApplySocialEffectsCore(TradeBot bot, SocialMessage message, List<TradeBot> witnesses, bool direct)
    {
        if (direct && message.targetId > 0 && SocialBots.TryGetValue(message.targetId, out var receiver))
        {
            double anger = Effect(message.act, "anger");
            if (message.memory && (message.act == SocialAct.MOCK || message.act == SocialAct.REBUT)) { anger *= Config.Get("memoryAngerScale"); }
            ChangeRelation(receiver, message.senderId, "anger", anger, "direct-message", message.id, message.roomId, threadId: message.threadId);
            ChangeRelation(receiver, message.senderId, "affinity", Effect(message.act, "affinity"), "direct-message", message.id, message.roomId);
            double fear = Effect(message.act, "fear");
            if (message.act != SocialAct.MOCK || CaptureFacts(receiver, FindSocialStock(message.facts?.symbol)).losing)
            { receiver.Social.Psychology.SocialDelta("fear", fear, "direct-message", message.id); }
        }
        var thread = FindSocialThread(message.threadId);
        if (direct && message.act == SocialAct.MEDIATE && thread != null) { Mediate(bot, message, thread); }
        if (thread != null && thread.kind == SocialThreadKind.ARGUMENT)
        {
            if (!fightWitnesses.TryGetValue(thread.id, out var seen)) { seen = new HashSet<int>(); fightWitnesses[thread.id] = seen; }
            foreach (var witness in witnesses) { seen.Add(witness.Social.Id); }
        }
        foreach (var witness in witnesses)
        {
            if (witness.Social.Id == message.targetId)
            { continue; }
            var mind = witness.Social.Psychology;
            ObserveRoomReferral(witness, bot, message);
            WitnessCompanyOpinion(witness, message);
            RememberWitnessedResult(witness, message);
            if (mind.AngerStage(message.senderId) >= 2 && mind.ActiveThread == 0 && !pendingReplies.ContainsKey(mind.SocialId) && !pendingOrigins.ContainsKey(mind.SocialId))
            {
                if (thread != null && thread.Active && thread.participants.Count < Config.Int("maxParticipants") && !thread.participants.Contains(mind.SocialId))
                {
                    mind.Intentions.Add(new SocialIntent { roomId = message.roomId, motive = SocialMotive.WITNESSED_EVENT, forcedAct = SocialAct.REBUT,
                        threadId = thread.id, targetId = message.senderId, facts = message.facts, due = SocialTime.Seconds + Config.Get("replyDelayMin"), expires = SocialTime.Seconds + Config.Get("witnessExpiry") });
                    thread.participants.Add(mind.SocialId); mind.ActiveThread = thread.id;
                }
                else if (thread == null && CanStartWith(witness, bot, message.roomId))
                { Offer(witness, message.roomId, SocialMotive.WITNESSED_EVENT, message.facts, SocialAct.REBUT, message.senderId, message.id); }
            }
            double multiplier = Config.Get("witnessHalfSuggestibility") + mind.Traits.Suggestibility / 100d;
            if (message.targetId > 0 && mind.Relation(message.targetId).affinity >= Config.Get("friendThreshold"))
            {
                double anger = message.act == SocialAct.MOCK ? Config.Get("friendMockAnger") : message.act == SocialAct.INSULT ? Config.Get("friendInsultAnger") : 0;
                WitnessRelation(witness, message, "anger", anger * multiplier);
            }
            if (message.act == SocialAct.INSULT) { WitnessRelation(witness, message, "affinity", Config.Get("insultWitnessAffinity") * multiplier); }
            if (message.act == SocialAct.LAMENT)
            {
                bool held = CaptureFacts(witness, FindSocialStock(message.facts?.symbol)).held;
                WitnessEmotion(witness, message, "fear", (held ? Config.Get("lamentHeldFear") : Config.Get("lamentOtherFear")) * multiplier);
            }
            if (message.act == SocialAct.BRAG)
            {
                var own = CaptureFacts(witness, FindSocialStock(message.facts?.symbol));
                decimal.TryParse(own.pct, System.Globalization.NumberStyles.Number, SocialCulture, out decimal ownReturn);
                decimal.TryParse(message.facts?.pct, System.Globalization.NumberStyles.Number, SocialCulture, out decimal authorReturn);
                if (authorReturn > ownReturn)
                {
                    WitnessEmotion(witness, message, "greed", Config.Get("bragGreed") * multiplier);
                    ConsiderSocialFomo(witness, message);
                }
                if (own.losing) { WitnessRelation(witness, message, "affinity", Config.Get("losingBragAffinity") * multiplier); }
            }
            if ((message.act == SocialAct.PRAISE_PLAYER || message.act == SocialAct.WARN_PLAYER) && message.facts.eventId > 0 && mind.SocialHeardEvents.Add(message.facts.eventId))
            { WitnessEmotion(witness, message, "trust", (message.act == SocialAct.PRAISE_PLAYER ? Config.Get("praiseTrust") : Config.Get("warnTrust")) * multiplier); }
        }
        if (direct && message.act == SocialAct.FAREWELL && bot.Social.Psychology.ExitAt >= 0 && SocialTime.Seconds >= bot.Social.Psychology.ExitAt)
        { DepartSocial(bot, message, witnesses); }
        ApplyModerationReaction(bot, message, witnesses);
    }
```

### Assets/Scripts/NightShift/ChatSocialSpeech.cs · RenderSocialText

원본:

```csharp
public static string RenderSocialText(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, string target, IEnumerable<SocialMessage> recent = null)
    {
        message.debug ??= new SocialMessageDebug();
        message.debug.speechDisposition = mind.Traits.SpeechLabel; message.debug.speechKey = null; message.debug.speechVoice = VoiceLabel(mind);
        var random = CosmeticRandom(mind, message, 0x58c43af1u);
        double communityChance = ChatSocialConfig.Current.Get(mind.Traits.Serious ? "communitySeriousChance" : "communityPlayfulChance");
        string phrase = ContextualSocialText(template, message, parent);
        bool community = false;
        if (random.Next() < communityChance)
        {
            var group = CommunityGroup(template, message, parent);
            if (group != null)
            {
                var candidates = new List<int>();
                for (int i = 0; i < group.texts.Length; i++)
                {
                    string key = SpeechChoiceKey(group, i);
                    bool repeated = mind.RecentSpeechKeys.Contains(key);
                    if (!repeated && recent != null)
                    { foreach (var previous in recent) { if (previous.debug?.speechKey == key) { repeated = true; break; } } }
                    if (repeated)
                    { continue; }
                    bool complete = true;
                    foreach (Match match in SlotPattern.Matches(group.texts[i]))
                    {
                        if (string.IsNullOrEmpty(SpeechSlot(message.facts, match.Groups[1].Value, target, 0)))
                        { complete = false; break; }
                    }
                    if (complete) { candidates.Add(i); }
                }
                if (candidates.Count > 0)
                {
                    int index = candidates[random.Range(0, candidates.Count)];
                    phrase = group.texts[index]; message.debug.speechKey = SpeechChoiceKey(group, index); community = true;
                }
            }
        }
        if (!community && mind.Traits.Serious && (message.facts.notice == "WATCH_RISE" || message.facts.notice == "WATCH_FALL" || message.facts.notice == "OPPONENT_LEFT" || IsMarketRiskNotice(message.facts.notice)))
        {
            string calmKey = template.key.Replace(".casual.", ".formal.").Replace(".rough.", ".formal.");
            foreach (var calm in ChatSocialAssets.Candidates(message.act, "formal", template.intensity, "plain"))
            {
                if (calm.key == calmKey) { phrase = calm.text; break; }
            }
        }
        string voiceText = null;
        bool voiced = !community && TryVoiceText(mind, template, message, parent, target, recent, out voiceText);
        if (voiced) { phrase = voiceText; }
        phrase = VoiceStockParticles(phrase, message.facts.stockName ?? message.facts.symbol);
        // Trading instructions retain exact limit prices, sizes and fill accounting. Only conversation P/L slots are abbreviated.
        bool colloquial = !message.facts.signal && (community || random.Next() < ChatSocialConfig.Current.Get(mind.Traits.Serious ? "seriousColloquialNumberChance" : "colloquialNumberChance"));
        int dialect = random.Range(0, 3);
        message.debug.numberStyle = colloquial ? "구어체·근사 손익" : "정확한 손익";
        if (colloquial)
        {
            phrase = SpokenNumberPattern.Replace(phrase, match =>
            {
                string name = match.Groups[1].Value, value = Slot(message.facts, name, target);
                return name == "amount" || name == "memAmount" ? ChatSocialNumbers.Amount(value, 0, signed: true, compactNoun: true) : ChatSocialNumbers.Return(value, dialect);
            });
        }
        string text = SlotPattern.Replace(phrase, match => SpeechSlot(message.facts, match.Groups[1].Value, target, dialect));
        return community || voiced ? text : DecorateSocialText(mind, message, text);
    }
```

수정본:

```csharp
public static string RenderSocialText(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, string target, IEnumerable<SocialMessage> recent = null)
    {
        message.debug ??= new SocialMessageDebug();
        message.debug.speechDisposition = mind.Traits.SpeechLabel; message.debug.speechKey = null; message.debug.speechVoice = VoiceLabel(mind);
        var random = CosmeticRandom(mind, message, 0x58c43af1u);
        double communityChance = ChatSocialConfig.Current.Get(mind.Traits.Serious ? "communitySeriousChance" : "communityPlayfulChance");
        string phrase = ContextualSocialText(template, message, parent);
        bool community = false;
        if (random.Next() < communityChance)
        {
            var group = CommunityGroup(template, message, parent);
            if (group != null)
            {
                var candidates = new List<int>();
                for (int i = 0; i < group.texts.Length; i++)
                {
                    string key = SpeechChoiceKey(group, i);
                    bool repeated = mind.RecentSpeechKeys.Contains(key);
                    if (!repeated && recent != null)
                    { foreach (var previous in recent) { if (previous.debug?.speechKey == key) { repeated = true; break; } } }
                    if (repeated)
                    { continue; }
                    bool complete = true;
                    foreach (Match match in SlotPattern.Matches(group.texts[i]))
                    {
                        if (string.IsNullOrEmpty(SpeechSlot(message.facts, match.Groups[1].Value, target, 0)))
                        { complete = false; break; }
                    }
                    if (complete) { candidates.Add(i); }
                }
                if (candidates.Count > 0)
                {
                    int index = candidates[random.Range(0, candidates.Count)];
                    phrase = group.texts[index]; message.debug.speechKey = SpeechChoiceKey(group, index); community = true;
                }
            }
        }
        if (!community && mind.Traits.Serious && (message.facts.notice == "WATCH_RISE" || message.facts.notice == "WATCH_FALL" || message.facts.notice == "OPPONENT_LEFT" || IsMarketRiskNotice(message.facts.notice) || IsCompanyNews(message.facts.notice)))
        {
            string calmKey = template.key.Replace(".casual.", ".formal.").Replace(".rough.", ".formal.");
            foreach (var calm in ChatSocialAssets.Candidates(message.act, "formal", template.intensity, "plain"))
            {
                if (calm.key == calmKey) { phrase = calm.text; break; }
            }
        }
        string voiceText = null;
        bool voiced = !community && TryVoiceText(mind, template, message, parent, target, recent, out voiceText);
        if (voiced) { phrase = voiceText; }
        phrase = VoiceStockParticles(phrase, message.facts.stockName ?? message.facts.symbol);
        // Trading instructions retain exact limit prices, sizes and fill accounting. Only conversation P/L slots are abbreviated.
        bool colloquial = !message.facts.signal && (community || random.Next() < ChatSocialConfig.Current.Get(mind.Traits.Serious ? "seriousColloquialNumberChance" : "colloquialNumberChance"));
        int dialect = random.Range(0, 3);
        message.debug.numberStyle = colloquial ? "구어체·근사 손익" : "정확한 손익";
        if (colloquial)
        {
            phrase = SpokenNumberPattern.Replace(phrase, match =>
            {
                string name = match.Groups[1].Value, value = Slot(message.facts, name, target);
                return name == "amount" || name == "memAmount" ? ChatSocialNumbers.Amount(value, 0, signed: true, compactNoun: true) : ChatSocialNumbers.Return(value, dialect);
            });
        }
        string text = SlotPattern.Replace(phrase, match => SpeechSlot(message.facts, match.Groups[1].Value, target, dialect));
        return community || voiced ? text : DecorateSocialText(mind, message, text);
    }
```

### Assets/Scripts/MarketDay/MarketDayController.cs · Sold

원본:

```csharp
public void Sold(Trader trader, Stock stock, decimal volume, decimal price, decimal basis)
    {
        if (!(trader is TradeBot bot) || bot.Social == null || volume <= 0m || basis <= 0m)
        { return; }

        var memory = Memory(bot, stock);
        if (memory == null)
        { return; }

        if (Time.time - memory.SellTime > recentTradeLifetime)
        {
            memory.SellVolume = 0m;
            memory.SellProfit = 0m;
        }

        decimal total = memory.SellVolume + volume;
        memory.SellPrice = (memory.SellPrice * memory.SellVolume + price * volume) / total;
        memory.SellVolume = total;
        memory.SellTime = Time.time;
        decimal profit = (price - basis) * volume;
        memory.SellProfit += profit;
        memory.PendingProfit += profit;
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
```

수정본:

```csharp
public void Sold(Trader trader, Stock stock, decimal volume, decimal price, decimal basis)
    {
        if (!(trader is TradeBot bot) || bot.Social == null || volume <= 0m || basis <= 0m)
        { return; }

        var memory = Memory(bot, stock);
        if (memory == null)
        { return; }

        if (Time.time - memory.SellTime > recentTradeLifetime)
        {
            memory.SellVolume = 0m;
            memory.SellProfit = 0m;
        }

        decimal total = memory.SellVolume + volume;
        memory.SellPrice = (memory.SellPrice * memory.SellVolume + price * volume) / total;
        memory.SellVolume = total;
        memory.SellTime = Time.time;
        decimal profit = (price - basis) * volume;
        bot.Social.Psychology.Companies.Realized(stock.symbol, (double)profit, (double)(basis * volume), Time.time);
        memory.SellProfit += profit;
        memory.PendingProfit += profit;
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
```

### Assets/Scripts/TradeBot/TradeBot.cs · BuyLimit

원본:

```csharp
public override void BuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        if (!CanBuyStock(ResolveStock(stock)) || WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this))
        { return; }

        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
        { return; }
        if (Social != null && Social.Psychology.HasFomoFor(ResolveStock(stock)))
        { volume = decimal.Floor(System.Math.Min(volume * (decimal)ChatSocialConfig.Current.Get("fomoAmountScale"), System.Math.Max(0m, cash - ReservedCash) / System.Math.Max(1m, price))); }
        MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), true, ref price, ref volume);
        if (!IsExchangeParticipant && Social != null)
        {
            double riskScale = Social.Psychology.MarketRiskBuyScale(ResolveStock(stock)?.symbol, Time.time);
            volume = decimal.Floor(volume * (decimal)riskScale);
            if (volume < 1m) { return; }
        }
        base.BuyLimit(price, volume, subTickSteps, stock);
    }
```

수정본:

```csharp
public override void BuyLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        if (!CanBuyStock(ResolveStock(stock)) || WorldDramaDirector.Instance != null && !WorldDramaDirector.Instance.CanFundBuy(this))
        { return; }

        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
        { return; }
        if (Social != null && Social.Psychology.HasFomoFor(ResolveStock(stock)))
        { volume = decimal.Floor(System.Math.Min(volume * (decimal)ChatSocialConfig.Current.Get("fomoAmountScale"), System.Math.Max(0m, cash - ReservedCash) / System.Math.Max(1m, price))); }
        MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), true, ref price, ref volume);
        if (!IsExchangeParticipant && Social != null)
        {
            double riskScale = Social.Psychology.MarketRiskBuyScale(ResolveStock(stock)?.symbol, Time.time);
            double newsScale = Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, true, Time.time);
            volume = decimal.Floor(volume * (decimal)(riskScale * newsScale));
            if (volume < 1m) { return; }
        }
        base.BuyLimit(price, volume, subTickSteps, stock);
    }
```

### Assets/Scripts/TradeBot/TradeBot.cs · SellLimit

원본:

```csharp
public override void SellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        if (!CanSellStock(ResolveStock(stock)))
        { return; }

        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
        { return; }
        MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), false, ref price, ref volume);
        base.SellLimit(price, volume, subTickSteps, stock);
    }
```

수정본:

```csharp
public override void SellLimit(decimal price, decimal volume, int subTickSteps = 0, Stock stock = null)
    {
        if (!MarketDayController.CanTrade || !isActiveAndEnabled)
        { return; }

        if (!CanSellStock(ResolveStock(stock)))
        { return; }

        if (InvestorSociety.Instance?.Orders != null && !InvestorSociety.Instance.Orders.AllowsRegular(this, ResolveStock(stock)))
        { return; }
        MarketRhythmDirector.Instance?.BoundOrder(this, ResolveStock(stock), false, ref price, ref volume);
        if (!IsExchangeParticipant && Social != null)
        { volume = decimal.Floor(volume * (decimal)Social.Psychology.Companies.TradeScale(ResolveStock(stock)?.symbol, false, Time.time)); }
        base.SellLimit(price, volume, subTickSteps, stock);
    }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs · Update

원본:

```csharp
void Update()
    {
        if (!panel.activeInHierarchy || Time.unscaledTime < next || StockManager.Instance == null || session.account == null)
        { return; }
        next = Time.unscaledTime + .3f;
        Stock stock = StockManager.Instance.CurrentStock;
        if (selected != stock)
        {
            selected = stock;
            if (!journal)
            {
                confirming = false;
                if (selected != null)
                {
                    session.Prepare(selected); receipt = false; draftSource = "";
                    price.SetTextWithoutNotify(PriceTickPolicy.RoundToTick(selected.currentPrice).ToString(CultureInfo.InvariantCulture));
                    quantity.SetTextWithoutNotify("1");
                }
            }
        }
        if (selected == null)
        { return; }
        if (!journal && selected != session.Target) { Prepare(); }
        Header();
        if (journal)
        { Journal(); }
        else
        { Order(); }
    }
```

수정본:

```csharp
void Update()
    {
        if (!panel.activeInHierarchy || Time.unscaledTime < next || StockManager.Instance == null || session.account == null)
        { return; }
        next = Time.unscaledTime + .3f;
        Stock stock = StockManager.Instance.CurrentStock;
        if (selected != stock)
        {
            selected = stock;
            if (!journal)
            {
                confirming = false;
                if (selected != null)
                {
                    session.Prepare(selected); receipt = false; draftSource = "";
                    price.SetTextWithoutNotify(PriceTickPolicy.RoundToTick(selected.currentPrice).ToString("0",CultureInfo.InvariantCulture));
                    quantity.SetTextWithoutNotify("1");
                }
            }
        }
        if (selected == null)
        { return; }
        if (!journal && selected != session.Target) { Prepare(); }
        if (journal && newsReader != null)
        {
            if (growthPanel.activeSelf && PlayerProgression.Instance != null) { visualGrowth?.Refresh(PlayerProgression.Instance); }
            return;
        }
        Header();
        if (journal)
        { Journal(); }
        else
        { Order(); }
    }
```

### Assets/Scripts/NightShift/TabletAccountUI.cs · Start

원본:

```csharp
void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        { int index = i; tabs[i].onClick.AddListener(() => ShowPage((Page)index)); }
        orderButton.onClick.AddListener(() => ShowPage(Page.Orders));
        autoButton.onClick.AddListener(() => ShowPage(Page.Automation));
        managementButton.onClick.AddListener(() => ShowPage(Page.Management));
        transferButton.onClick.AddListener(OpenTransfer);
        transferCancel.onClick.AddListener(() => transferPanel.SetActive(false));
        transferSubmit.onClick.AddListener(Transfer);
        accounts.Unlocked += Unlock;
        DeviceTerminalUI.Set(accountTitle,second ? "2번 계좌 · 자동 운용" : "1번 계좌 · 내 투자");
        lockRoot.SetActive(Locked); contentRoot.SetActive(!Locked);
        ShowPage(Page.Dashboard);
    }
```

수정본:

```csharp
void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        { int index = i; tabs[i].onClick.AddListener(() => ShowPage((Page)index)); }
        orderButton.onClick.AddListener(() => ShowPage(Page.Orders));
        autoButton.onClick.AddListener(() => ShowPage(Page.Automation));
        managementButton.onClick.AddListener(() => ShowPage(Page.Management));
        transferButton.onClick.AddListener(OpenTransfer);
        transferCancel.onClick.AddListener(() => transferPanel.SetActive(false));
        transferSubmit.onClick.AddListener(Transfer);
        accounts.Unlocked += Unlock;
        DeviceTerminalUI.Set(accountTitle,second ? "2번 계좌 · 자동 운용" : "1번 계좌 · 내 투자");
        lockRoot.SetActive(Locked); contentRoot.SetActive(!Locked);
        events.newsReader = NewsDeskUI.Install(this);
        TabletControlStyle.Apply(this);
        ShowPage(Page.Dashboard);
    }
```

### Assets/Scripts/NightShift/DeviceMarketJournal.cs · Add

원본:

```csharp
private void Add(Stock stock, long id, string title, string source, string certainty, bool urgent)
    {
        Flow(stock, out decimal volume, out decimal buy);
        Events.Insert(0, new Entry { Stock = stock, Id = id, Title = title, Source = source, Certainty = certainty, Urgent = urgent, Time = Stamp(), Volume = volume, BuyVolume = buy });
        if (Events.Count > 40)
        { Events.RemoveAt(40); }
        Version++;
    }
```

수정본:

```csharp
private void Add(Stock stock, long id, string title, string source, string certainty, bool urgent)
    {
        Flow(stock, out decimal volume, out decimal buy);
        Events.Insert(0, new Entry { Stock = stock, Id = id, Title = title, Source = source, Certainty = certainty, Urgent = urgent, Time = Stamp(), Volume = volume, BuyVolume = buy,
            Article = id > 0 ? NewsManager.Instance?.FindArticle(id) : null });
        if (Events.Count > 40)
        { Events.RemoveAt(40); }
        Version++;
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · Capture

원본:

```csharp
public string Capture()
    {
        return JsonUtility.ToJson(new Snapshot { worldId = WorldId, turnover = Turnover.ToString(CultureInfo.InvariantCulture), realizedProfit = RealizedProfit.ToString(CultureInfo.InvariantCulture), roomsOpened = CanOperateRooms,
            invitedRoomMask = InvestorSociety.Instance?.InvitedRoomMask ?? 0 });
    }
```

수정본:

```csharp
public string Capture()
    {
        return JsonUtility.ToJson(new Snapshot { worldId = WorldId, turnover = Turnover.ToString(CultureInfo.InvariantCulture), realizedProfit = RealizedProfit.ToString(CultureInfo.InvariantCulture), roomsOpened = CanOperateRooms,
            invitedRoomMask = InvestorSociety.Instance?.InvitedRoomMask ?? 0, newsCooldownRemaining = FakeNewsManager.Instance?.PlayerCooldown ?? 0, newsSavedUtc = DateTime.UtcNow.Ticks });
    }
```

### Assets/Scripts/NightShift/PlayerProgression.cs · Restore

원본:

```csharp
public bool Restore(string json, string restoredWorldId)
    {
        // A progress fragment is not a world save. Never grant a new economy the previous world's rights.
        Snapshot saved;
        try { saved = JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return false; }
        if (saved == null || saved.version != 1 || saved.worldId != WorldId || restoredWorldId != WorldId || saved.invitedRoomMask < 0 || saved.invitedRoomMask >= 1 << InvestorSociety.PlayerRoomCount ||
            !decimal.TryParse(saved.turnover, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal turnover) || turnover < 0m ||
            !decimal.TryParse(saved.realizedProfit, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal profit))
        { return false; }
        Turnover = turnover; RealizedProfit = profit; CanOperateRooms = saved.roomsOpened;
        InvestorSociety.Instance?.RestoreInvitedRooms(saved.invitedRoomMask);
        return true;
    }
```

수정본:

```csharp
public bool Restore(string json, string restoredWorldId)
    {
        // A progress fragment is not a world save. Never grant a new economy the previous world's rights.
        Snapshot saved;
        try { saved = JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return false; }
        if (saved == null || saved.version != 1 || saved.worldId != WorldId || restoredWorldId != WorldId || saved.invitedRoomMask < 0 || saved.invitedRoomMask >= 1 << InvestorSociety.PlayerRoomCount ||
            !decimal.TryParse(saved.turnover, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal turnover) || turnover < 0m ||
            !decimal.TryParse(saved.realizedProfit, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal profit))
        { return false; }
        if (double.IsNaN(saved.newsCooldownRemaining) || double.IsInfinity(saved.newsCooldownRemaining) || saved.newsCooldownRemaining < 0 || saved.newsCooldownRemaining > 600 ||
            saved.newsSavedUtc < 0 || saved.newsSavedUtc > DateTime.MaxValue.Ticks)
        { return false; }
        Turnover = turnover; RealizedProfit = profit; CanOperateRooms = saved.roomsOpened;
        FakeNewsManager.Instance?.RestorePlayerCooldown(saved.newsCooldownRemaining, saved.newsSavedUtc);
        InvestorSociety.Instance?.RestoreInvitedRooms(saved.invitedRoomMask);
        return true;
    }
```

### Assets/Scripts/NightShift/PendingOrderListUI.cs · CreateRow

원본:

```csharp
private Row CreateRow(int index)
    {
        var row = new Row { Root = Rect("Panel :: Pending Order " + index,content,0,0,1,1) };
        row.Root.anchorMin = new Vector2(0,1); row.Root.anchorMax = Vector2.one;
        row.Root.pivot = new Vector2(.5f,1); row.Root.sizeDelta = new Vector2(0,RowHeight);
        row.Root.anchoredPosition = new Vector2(0,-index * RowHeight);
        row.Name = Label("Pending Stock",row.Root,29f,.015f,.035f,.78f,.29f);
        row.Detail = Label("Pending Price Quantity",row.Root,27f,.015f,.34f,.78f,.29f);
        row.State = Label("Pending Fill State",row.Root,23f,.015f,.66f,.78f,.25f);
        row.State.color = new Color32(166,177,186,255);
        var divider = Rect("Image :: Pending Divider",row.Root,0,.985f,1,.008f).gameObject.AddComponent<Image>();
        divider.color = new Color32(52,67,75,180); divider.raycastTarget = false;
        var buttonRoot = Rect("Button :: Cancel Pending Order",row.Root,.82f,.17f,.18f,.61f);
        var image = buttonRoot.gameObject.AddComponent<Image>(); image.color = new Color32(31,44,50,255);
        var button = buttonRoot.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Label("Cancel Pending Order",buttonRoot,27f,0,0,1,1);
        label.alignment = TextAlignmentOptions.Center; label.text = "취소";
        button.onClick.AddListener(() => { session.CancelPending(row.Entry); Refresh(); });
        return row;
    }
```

수정본:

```csharp
private Row CreateRow(int index)
    {
        var row = new Row { Root = Rect("Panel :: Pending Order " + index,content,0,0,1,1) };
        row.Root.anchorMin = new Vector2(0,1); row.Root.anchorMax = Vector2.one;
        row.Root.pivot = new Vector2(.5f,1); row.Root.sizeDelta = new Vector2(0,RowHeight);
        row.Root.anchoredPosition = new Vector2(0,-index * RowHeight);
        row.Name = Label("Pending Stock",row.Root,29f,.015f,.035f,.78f,.29f);
        row.Detail = Label("Pending Price Quantity",row.Root,27f,.015f,.34f,.78f,.29f);
        row.State = Label("Pending Fill State",row.Root,23f,.015f,.66f,.78f,.25f);
        row.State.color = new Color32(166,177,186,255);
        var divider = Rect("Image :: Pending Divider",row.Root,0,.985f,1,.008f).gameObject.AddComponent<Image>();
        divider.color = new Color32(52,67,75,180); divider.raycastTarget = false;
        var buttonRoot = Rect("Button :: Cancel Pending Order",row.Root,.82f,.17f,.18f,.61f);
        var image = buttonRoot.gameObject.AddComponent<Image>(); image.color = new Color32(31,44,50,255);
        var button = buttonRoot.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Label("Cancel Pending Order",buttonRoot,27f,0,0,1,1);
        label.alignment = TextAlignmentOptions.Center; label.text = "취소";
        TabletControlStyle.Button(button);
        button.onClick.AddListener(() => { session.CancelPending(row.Entry); Refresh(); });
        return row;
    }
```

### Assets/Scripts/NightShift/CompactGrowthUI.cs · Configure

원본:

```csharp
public void Configure(Button action,Button close)
    {
        if (configured)
        { return; }
        configured = true; primary = action; actionLabel = action.GetComponentInChildren<TextMeshProUGUI>();
        Layout((RectTransform)transform,.015f,.012f,.97f,.976f);
        Layout(title.rectTransform,.045f,.04f,.74f,.075f); title.fontSize = 52;
        Layout((RectTransform)details.transform,.83f,.045f,.125f,.06f);
        var detailsLabel = details.GetComponentInChildren<TextMeshProUGUI>(); detailsLabel.text = "조건 보기"; detailsLabel.fontSize = 30;
        nextStep = Caption("Next Step",.045f,.15f,.91f,.085f,49);
        hint = Caption("Action Hint",.045f,.25f,.91f,.055f,31); hint.color = new Color32(166,177,186,255);
        Layout((RectTransform)overview.transform,.045f,.35f,.91f,.36f);
        Layout(turnover.rectTransform,.10f,0,.26f,.42f); Layout(profit.rectTransform,.64f,0,.26f,.42f);
        turnoverCaption = overview.transform.Find("Text :: Turnover Caption").GetComponent<TextMeshProUGUI>();
        profitCaption = overview.transform.Find("Text :: Profit Caption").GetComponent<TextMeshProUGUI>();
        turnoverCaption.fontSize = profitCaption.fontSize = 32;
        turnoverAmount.fontSize = profitAmount.fontSize = 38;
        Layout(detailText.rectTransform,.045f,.35f,.91f,.38f); detailText.fontSize = 32;
        Layout((RectTransform)action.transform,.045f,.80f,.66f,.115f); actionLabel.fontSize = 43;
        Layout((RectTransform)close.transform,.73f,.82f,.225f,.085f); close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;
        primary.image.color = new Color32(18,124,137,255);
        primary.transition = Selectable.Transition.ColorTint;
    }
```

수정본:

```csharp
public void Configure(Button action,Button close)
    {
        if (configured)
        { return; }
        configured = true; primary = action; actionLabel = action.GetComponentInChildren<TextMeshProUGUI>();
        Layout((RectTransform)transform,.015f,.012f,.97f,.976f);
        Layout(title.rectTransform,.045f,.04f,.74f,.075f); title.fontSize = 52;
        Layout((RectTransform)details.transform,.83f,.045f,.125f,.06f);
        var detailsLabel = details.GetComponentInChildren<TextMeshProUGUI>(); detailsLabel.text = "조건 보기"; detailsLabel.fontSize = 30;
        nextStep = Caption("Next Step",.045f,.15f,.91f,.085f,49);
        hint = Caption("Action Hint",.045f,.25f,.91f,.055f,31); hint.color = new Color32(166,177,186,255);
        Layout((RectTransform)overview.transform,.045f,.35f,.91f,.36f);
        Layout(turnover.rectTransform,.10f,0,.26f,.42f); Layout(profit.rectTransform,.64f,0,.26f,.42f);
        turnoverCaption = overview.transform.Find("Text :: Turnover Caption").GetComponent<TextMeshProUGUI>();
        profitCaption = overview.transform.Find("Text :: Profit Caption").GetComponent<TextMeshProUGUI>();
        turnoverCaption.fontSize = profitCaption.fontSize = 32;
        turnoverAmount.fontSize = profitAmount.fontSize = 38;
        Layout(detailText.rectTransform,.045f,.35f,.91f,.38f); detailText.fontSize = 32;
        Layout((RectTransform)action.transform,.045f,.80f,.66f,.115f); actionLabel.fontSize = 43;
        Layout((RectTransform)close.transform,.73f,.82f,.225f,.085f); close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;
        TabletControlStyle.Button(primary,true);
        primary.transition = Selectable.Transition.ColorTint;
    }
```

## 연결 의도

- NewsManager/FakeNewsManager: 기존 뉴스 이벤트·관심도 경로를 유지하며 본문과 발행 제한을 추가한다. 가짜 뉴스로 realValue/currentPrice를 직접 대입하지 않는다.
- 기업 심리: 실제 독자·실제 매도 성과·실제 채팅 목격으로만 변화하며 플레이어 신뢰/배신과 별개다.
- 주문: 기존 한도·예약금·실제 체결은 유지하고 일반 봇의 다음 매수/매도 수량에 제한된 뉴스 해석을 반영한다.
- 채팅: 큐·타이핑·쿨다운·화행 검증을 재사용하며 게시 직전 자신의 기사 경험을 재검증한다.
- UI: 기존 Canvas 내부에서 목록 풀을 재사용한다. 뉴스 조회/필터로 주문·가격을 변경하지 않는다. 발행은 명시적인 버튼 경로만 사용한다.
- 정수 가격: DeviceTerminalUI.Update의 최초 가격 입력을 ToString("0", InvariantCulture)로 통일해 decimal 내부 scale의 소수점 0들이 입력 검사에 걸리던 오류를 수정한다.
- 스킨: 생성된 PNG 3개의 캐시된 9-slice Sprite를 기존 Button/Input에 지정한다. 설명은 TMP 텍스트, 선택 행·상단 탐색 탭은 기존 역할을 유지한다. Hover/Pressed/Disabled는 Unity Selectable 전환이며 per-frame 컴포넌트 검색이 없다.
