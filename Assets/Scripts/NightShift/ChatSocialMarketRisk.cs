using System;
using System.Collections.Generic;

public sealed partial class InvestorPsychology
{
    private sealed class MarketRiskMemory
    {
        public int version, grade, peak;
        public long eventId;
        public double buyScale = 1, pauseUntil, at;
    }
    private readonly Dictionary<string, MarketRiskMemory> marketRisk = new();
    internal long LastRiskBulletin;

    // This is the bot's observed risk, not an omniscient read of the current exchange grade.
    public double MarketRiskBuyScale(string symbol, double seconds)
    {
        if (symbol == null || !marketRisk.TryGetValue(symbol, out var memory) || seconds < memory.at || seconds - memory.at >= ChatSocialConfig.Current.Get("riskAttentionSeconds"))
        { return 1; }
        return seconds < memory.pauseUntil ? 0 : memory.buyScale;
    }
    public bool ObserveMarketRisk(string symbol, int version, int grade, long eventId, double seconds, double preference, bool held)
    {
        if (string.IsNullOrEmpty(symbol) || grade < 0 || grade > 4)
        { return false; }
        marketRisk.TryGetValue(symbol, out var memory);
        if (memory != null && version <= memory.version)
        { return false; }
        if (memory == null)
        {
            if (marketRisk.Count >= 16)
            {
                string oldest = null; double at = double.MaxValue;
                foreach (var row in marketRisk) { if (row.Value.at < at) { at = row.Value.at; oldest = row.Key; } }
                if (oldest != null) { marketRisk.Remove(oldest); }
            }
            memory = new MarketRiskMemory(); marketRisk[symbol] = memory;
        }
        var config = ChatSocialConfig.Current;
        double caution = Math.Max(.05, Math.Min(1, (1 - Math.Max(0, Math.Min(1, preference))) * .65 + Fear / 100d * .25 + (100 - Greed) / 100d * .2 + (held ? .15 : 0)));
        int severity = Math.Min(3, grade);
        // A fluctuating warning must not pump the same fear repeatedly.
        double fear = Math.Max(0, severity - memory.peak) * config.Get("riskFearPerGrade") * caution;
        memory.peak = Math.Max(memory.peak, severity);
        memory.version = version; memory.grade = grade; memory.eventId = eventId; memory.at = seconds;
        bool dismissive = caution < .35 && Greed >= 60;
        memory.buyScale = dismissive ? 1 : Math.Max(.1, 1 - severity / 3d * caution * config.Get("riskBuyReduction"));
        memory.pauseUntil = grade >= 3 && caution >= config.Get("riskPauseConcern") ? seconds + config.Get("riskPauseSeconds") : 0;
        if (grade == 0) { memory.buyScale = 1; memory.pauseUntil = 0; }
        SocialDelta("fear", fear, "market-risk-observed", eventId);
        LastCause = "위험 알림 확인: " + symbol + " · " + InvestorSociety.MarketRiskLabel(grade);
        ChatSocialAudit.Journal.Event(new SocialEvent { id = eventId, time = ChatSocialAudit.GameMinute, actor = SocialId,
            kind = "MARKET_RISK_OBSERVED", reason = symbol + " " + InvestorSociety.MarketRiskLabel(grade), value = MarketRiskBuyScale(symbol, seconds) });
        return true;
    }
}

public sealed partial class InvestorSociety
{
    private sealed class MarketRiskBulletin
    {
        public Stock stock;
        public int version, grade, offered;
        public long id;
        public double expires;
    }
    private readonly List<MarketRiskBulletin> riskBulletins = new();
    private readonly Dictionary<int, double> nextRiskSpeech = new();
    public static string MarketRiskLabel(int grade) => grade switch { 0 => "안전", 1 => "경고", 2 => "위험", 3 => "고위험", _ => "거래중지" };
    public static bool IsMarketRiskNotice(string notice) => notice != null && notice.StartsWith("MARKET_RISK_", StringComparison.Ordinal);

    public long NotifyMarketRisk(Stock stock)
    {
        if (stock == null || stock.RiskPending || stock.IsDelisted)
        { return 0; }
        foreach (var prior in riskBulletins)
        { if (prior.stock == stock && prior.version == stock.RiskVersion) { return prior.id; } }
        double now = SessionClock.Time;
        riskBulletins.RemoveAll(row => row.stock == stock || row.expires < now || row.stock.IsDelisted);
        if (riskBulletins.Count >= 16) { riskBulletins.RemoveAt(0); }
        var bulletin = new MarketRiskBulletin { stock = stock, version = stock.RiskVersion, grade = (int)stock.RiskGrade,
            id = ChatSocialIds.Next(), expires = now + Config.Get("riskBulletinSeconds") };
        riskBulletins.Add(bulletin);
        ChatSocialAudit.Journal.Event(new SocialEvent { id = bulletin.id, time = SocialTime.GameMinute, kind = "MARKET_RISK_NOTICE",
            reason = stock.symbol + " " + MarketRiskLabel(bulletin.grade), value = bulletin.grade });
        return bulletin.id;
    }
    private void TickMarketRisk(TradeBot bot)
    {
        var mind = bot.Social.Psychology;
        if (!Present(bot, 0) || !mind.ActiveAt((float)SocialTime.Seconds))
        { return; }
        foreach (var bulletin in riskBulletins)
        {
            if (bulletin.id <= mind.LastRiskBulletin) { continue; }
            mind.LastRiskBulletin = bulletin.id;
            var stock = bulletin.stock;
            if (bulletin.expires < SocialTime.Seconds || stock.IsDelisted || stock.RiskVersion != bulletin.version)
            { continue; }
            bool held = bot.GetOwnedVolume(stock) > 0;
            bool interested = held || mind.WatchedStock == stock;
            if (!interested || mind.SocialRng.Next() >= Config.Get("riskWitnessChance") * (.5 + mind.Activity * .5))
            { continue; }
            if (!mind.ObserveMarketRisk(stock.symbol, bulletin.version, bulletin.grade, bulletin.id, SocialTime.Seconds, bot.riskPreference, held))
            { continue; }
            int room = mind.Room;
            bool roomBusy = nextRiskSpeech.TryGetValue(room, out double next) && SocialTime.Seconds < next;
            if (roomBusy || bulletin.offered >= Config.Int("riskVoicesPerNotice") || !Present(bot, room) ||
                mind.SocialRng.Next() >= Config.Get("riskSpeechChance") * (.5 + mind.Traits.Sociability / 100d))
            { continue; }
            var facts = CaptureFacts(bot, stock);
            facts.eventId = bulletin.id; facts.riskVersion = bulletin.version; facts.riskGrade = bulletin.grade;
            facts.riskObserver = mind.SocialId;
            SetRiskStance(mind, facts);
            Offer(bot, room, SocialMotive.SELF_EVENT, facts, SocialAct.REACT, cause: bulletin.id);
            bulletin.offered++; nextRiskSpeech[room] = SocialTime.Seconds + Config.Get("riskSpeechGapSeconds");
        }
    }
    private void SetRiskStance(InvestorPsychology mind, SocialFacts facts)
    {
        double scale = mind.MarketRiskBuyScale(facts.symbol, SocialTime.Seconds);
        facts.notice = "MARKET_RISK_" + (facts.riskGrade == 0 ? "EASED" : facts.riskGrade == 4 ? "HALTED" : scale == 0 ? "PAUSE" : scale < .8 ? "CAUTIOUS" : scale < 1 ? "WATCH" : "DISMISS");
    }
    private bool RefreshMarketRiskFacts(TradeBot bot, SocialIntent intent, SocialFacts current)
    {
        var offered = intent.facts;
        if (!IsMarketRiskNotice(offered?.notice)) { return true; }
        // Replies may discuss a warning, but cannot borrow another bot's observation or trading stance.
        if (intent.replyToId != 0 || offered.riskObserver != bot.Social.Id)
        { current.notice = null; return true; }
        var stock = FindSocialStock(offered.symbol);
        if (stock == null || stock.IsDelisted || stock.RiskVersion != offered.riskVersion)
        { return false; }
        current.riskVersion = offered.riskVersion; current.riskGrade = offered.riskGrade; current.riskObserver = offered.riskObserver;
        SetRiskStance(bot.Social.Psychology, current);
        return true;
    }
}
