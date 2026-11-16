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
