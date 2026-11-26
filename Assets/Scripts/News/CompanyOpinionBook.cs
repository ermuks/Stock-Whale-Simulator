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
        /// <summary>
        /// 기대값(anchor)과 눈앞의 느낌(quick). belief는 이 둘을 섞은 값이고 매매는 belief로 한다.
        ///  - anchor: 이 종목이 "얼마짜리"라고 생각하는 값. 기사·소문·공포로 움직이고, 시세는 아주 천천히만 따라간다. 화면의 "시장이 믿는 가격"은 이 값의 평균이다.
        ///  - quick: 방금 본 시세와 흐름에서 나온 값. 예전의 belief가 이것이었다(볼 때마다 시세 쪽으로 빠르게 끌려간다).
        /// 예전 저장에는 없어서 0으로 읽히고, 처음 볼 때 belief로 채운다.
        /// </summary>
        public double anchor, quick;
        /// <summary>시세가 기대보다 5% 넘게 싼 채로 이어진 시간(초)과, 마지막으로 시세를 본 때. 저장하지 않는다(불러오면 다시 센다).</summary>
        [NonSerialized] public double below, seenAt;
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
        if (value.belief > 0) { Scale(value, 1 + Clamp(interpretation * .14, -.2, .2), now); }
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
        if (value.belief > 0) { Scale(value, 1 + Clamp(opinion, -1, 1) * .015, now); }
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
        if (value.anchor <= 0 || !Finite(value.anchor)) { value.anchor = value.belief; }
        if (value.quick <= 0 || !Finite(value.quick)) { value.quick = value.belief; }
        return value.belief;
    }
    /// <summary>믿는 값을 통째로 몇 % 옮긴다(기사, 소문, 공포, 세력의 흉흉한 소문). 기대값도 눈앞의 느낌도 같이 움직인다.</summary>
    private static void Scale(Opinion value, double factor, double now)
    {
        if (value.anchor <= 0 || !Finite(value.anchor)) { value.anchor = value.belief; }
        if (value.quick <= 0 || !Finite(value.quick)) { value.quick = value.belief; }
        value.belief = Math.Max(1, value.belief * factor); value.anchor = Math.Max(1, value.anchor * factor); value.quick = Math.Max(1, value.quick * factor);
        value.beliefAt = now;
    }
    /// <summary>
    /// 기대값이 belief에서 차지하는 몫(0~1). 0이면 예전과 같다(시세를 보는 대로 믿음이 따라가서, 시세가 기대에 닿기 전에 기대가 시세로 내려온다).
    /// 절반이면: 시세가 기대보다 20% 싸도 이 봇은 "10%쯤 싸다"고 느끼고, 그 느낌은 시세가 기대에 닿을 때까지 사라지지 않는다.
    /// </summary>
    public const double AnchorWeight = .5;
    /// <summary>
    /// 볼 때마다 기대값이 시세 쪽으로 옮겨 가는 몫. 한 봇이 한 종목을 하루(900초)에 스무 번쯤 본다.
    ///  - 시세가 기대보다 아래일 때(AnchorFollowDown): 아주 천천히. 하루에 차이의 15%쯤만 내려온다. 그래서 "기대보다 싼 종목"은 시세가 올라와서 차이가 메워진다.
    ///  - 시세가 기대보다 위일 때(AnchorFollowUp): 조금 더 빨리(하루에 30%쯤). 오른 값에 사람들이 익숙해진다.
    ///  - 급등주의 열병이 돌 때(fever, 0~1): 훨씬 빨리 따라 올라간다. 오르는 종목은 기대도 같이 올라야 랠리가 이어진다.
    /// 공포(패닉)와 나쁜 기사는 이것과 따로 기대값을 직접 깎는다(Scale). 진짜 폭락에서는 기대도 내려간다.
    /// </summary>
    public const double AnchorFollowDown = .008, AnchorFollowUp = .02, AnchorFollowFever = .25;
    /// <summary>이 시간(초)을 넘겨서도 시세가 기대에 못 미치면 기대가 꺾이기 시작한다.</summary>
    public const double DisappointAfter = 150;
    /// <summary>직접 본 가격과 흐름으로 믿음을 옮긴다. projection은 본 가격 대비 이 봇이 기대하는 비율, pull은 0~1 반영 속도.</summary>
    public void ObservePrice(string symbol, double price, double projection, double pull, double now, double fever = 0)
    {
        if (!Finite(price) || price <= 0 || !Finite(projection) || !Finite(pull)) { return; }
        var value = Read(symbol);
        if (value == null) { return; }
        Belief(symbol, price, now);
        // 눈앞의 느낌: 예전의 믿음과 같은 식으로, 본 시세와 흐름을 빠르게 따라간다.
        double target = price * (1 + Clamp(projection, -.4, .6));
        value.quick += (target - value.quick) * Clamp(pull * (1 - value.conviction / 250), .02, 1);
        value.quick = Math.Max(1, value.quick);
        // 기대값: 시세를 천천히만 따라간다. 내려가는 쪽이 특히 느리다.
        // 기다려도 안 오면 기대가 꺾인다: 시세가 기대보다 4% 넘게 싼 채로 오래(2분 반 넘게) 머물면 내려가는 속도가 점점 붙는다(15분쯤이면 일곱 배).
        // (측정: 물량이 쏟아져 하루 내내 밀린 대형주에서, 이 규칙이 느릴 때는 기대와 시세가 10%까지 벌어진 채 좁혀지지 않았다.)
        // 잠깐 눌린 종목은 시세가 올라와서 차이가 메워지지만, 파는 쪽이 끈질겨서(대주주의 처분, 진짜 가치를 아는 돈) 끝내 못 올라오는 종목은
        // 기대가 내려와서 메워진다. 그래야 화면의 "시장이 믿는 가격"이 닿지 못할 숫자로 오래 남지 않는다.
        double waited = Math.Min(120, Math.Max(0, now - (value.seenAt > 0 ? value.seenAt : now))); value.seenAt = now;
        value.below = price < value.anchor * .96 ? value.below + waited : Math.Max(0, value.below - waited * 2);
        double tired = 1 + Clamp((value.below - DisappointAfter) / DisappointAfter, 0, 6);
        double follow = price >= value.anchor ? AnchorFollowUp + Clamp(Finite(fever) ? fever : 0, 0, 1) * AnchorFollowFever : AnchorFollowDown * tired;
        value.anchor = Math.Max(1, value.anchor + (price - value.anchor) * follow);
        // 매매에 쓰는 믿음은 둘을 섞은 값이다(비율로 섞는다).
        value.belief = Math.Max(1, Math.Exp(AnchorWeight * Math.Log(value.anchor) + (1 - AnchorWeight) * Math.Log(value.quick))); value.beliefAt = now;
    }
    public void ShiftBelief(string symbol, double fraction, double now)
    {
        var value = Read(symbol);
        if (value == null || value.belief <= 0 || !Finite(fraction)) { return; }
        Scale(value, 1 + Clamp(fraction, -.3, .3), now);
    }
    /// <summary>이 봇의 기대값(화면의 "시장이 믿는 가격"을 만들 때 쓴다). 아직 이 종목을 본 적이 없으면 false.</summary>
    public bool TryAnchor(string symbol, out double anchor)
    {
        anchor = 0;
        if (string.IsNullOrEmpty(symbol) || !opinions.TryGetValue(symbol, out var value) || value.belief <= 0) { return false; }
        anchor = value.anchor > 0 && Finite(value.anchor) ? value.anchor : value.belief; return true;
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
                belief = row.belief, beliefAt = row.beliefAt, anchor = row.anchor, quick = row.quick,
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
                    anchor = Finite(row.anchor) && row.anchor > 0 ? row.anchor : 0, quick = Finite(row.quick) && row.quick > 0 ? row.quick : 0,
                    seen = new List<long>(row.seen), rumors = new List<long>(row.rumors) });
            }
        }
        return true;
    }
}
