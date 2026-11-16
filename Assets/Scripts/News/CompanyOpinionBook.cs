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
        /// <summary>이 봇이 믿는 이 종목의 가격. 진짜 가치가 아니라 본 것·읽은 것·들은 것으로 만든 환각이다.</summary>
        public double belief, beliefAt;
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
        // 읽은 기사는 진위와 무관하게 이 봇이 믿는 가격을 그 봇의 해석만큼 움직인다.
        if (value.belief > 0) { value.belief *= 1 + Clamp(interpretation * .14, -.2, .2); value.beliefAt = now; }
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
        if (value.belief > 0) { value.belief *= 1 + Clamp(opinion, -1, 1) * .015; value.beliefAt = now; }
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
    /// <summary>믿는 가격. 처음 보는 종목은 본 가격에 호감과 아직 식지 않은 뉴스 충동을 얹어 시작한다.</summary>
    public double Belief(string symbol, double price, double now)
    {
        if (!Finite(price) || price <= 0) { return 0; }
        var value = Read(symbol);
        if (value == null) { return price; }
        if (value.belief <= 0 || !Finite(value.belief))
        { value.belief = price * (1 + value.affinity / 100 * .05 + Impulse(value, now) * .14); value.beliefAt = now; }
        return value.belief;
    }
    /// <summary>직접 본 가격과 흐름으로 믿음을 옮긴다. projection은 본 가격 대비 이 봇이 기대하는 비율, pull은 0~1 반영 속도.</summary>
    public void ObservePrice(string symbol, double price, double projection, double pull, double now)
    {
        if (!Finite(price) || price <= 0 || !Finite(projection) || !Finite(pull)) { return; }
        var value = Read(symbol);
        if (value == null) { return; }
        if (value.belief <= 0 || !Finite(value.belief)) { Belief(symbol, price, now); }
        double target = price * (1 + Clamp(projection, -.4, .6));
        value.belief += (target - value.belief) * Clamp(pull * (1 - value.conviction / 250), .02, 1);
        value.belief = Math.Max(1, value.belief); value.beliefAt = now;
    }
    public void ShiftBelief(string symbol, double fraction, double now)
    {
        var value = Read(symbol);
        if (value == null || value.belief <= 0 || !Finite(fraction)) { return; }
        value.belief = Math.Max(1, value.belief * (1 + Clamp(fraction, -.3, .3))); value.beliefAt = now;
    }
    public bool TryBelief(string symbol, out double belief)
    {
        belief = 0;
        if (string.IsNullOrEmpty(symbol) || !opinions.TryGetValue(symbol, out var value) || value.belief <= 0) { return false; }
        belief = value.belief; return true;
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
                belief = row.belief, beliefAt = row.beliefAt,
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
                    belief = Finite(row.belief) && row.belief > 0 ? row.belief : 0, beliefAt = Finite(row.beliefAt) ? row.beliefAt : 0,
                    seen = new List<long>(row.seen), rumors = new List<long>(row.rumors) });
            }
        }
        return true;
    }
}
