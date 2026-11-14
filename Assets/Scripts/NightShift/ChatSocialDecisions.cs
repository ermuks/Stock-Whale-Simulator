using System;
using System.Collections.Generic;

public sealed partial class InvestorPsychology
{
    public string FomoSymbol;
    public double LastQuadrantReview = -1, LastFomoObservation = -1;
    public double LastRelationshipSpeech = -1;
    public bool HasFomo => FomoSymbol != null && ChatSocialAudit.GameMinute < FomoUntil;
    public bool HasFomoFor(Stock stock) => HasFomo && stock != null && stock.symbol == FomoSymbol;
    public string QuadrantLabel => Quadrant switch { InvestorQuadrant.Loyal => "충성", InvestorQuadrant.Ambivalent => "애증", InvestorQuadrant.Hostile => "적대", _ => "무관심" };
    public void RefreshQuadrant()
    {
        var c = ChatSocialConfig.Current;
        bool highTrust = Quadrant == InvestorQuadrant.Loyal || Quadrant == InvestorQuadrant.Ambivalent;
        bool highBetrayal = Quadrant == InvestorQuadrant.Hostile || Quadrant == InvestorQuadrant.Ambivalent;
        double high = c.Get("quadrantThreshold") + c.Get("quadrantHysteresis"), low = c.Get("quadrantThreshold") - c.Get("quadrantHysteresis");
        if (Trust >= high) { highTrust = true; } else if (Trust <= low) { highTrust = false; }
        if (Betrayal >= high) { highBetrayal = true; } else if (Betrayal <= low) { highBetrayal = false; }
        var before = Quadrant;
        Quadrant = highTrust ? highBetrayal ? InvestorQuadrant.Ambivalent : InvestorQuadrant.Loyal : highBetrayal ? InvestorQuadrant.Hostile : InvestorQuadrant.Indifferent;
        if (before != Quadrant)
        { ChatSocialAudit.Journal.Event(new SocialEvent { time = ChatSocialAudit.GameMinute, actor = SocialId, kind = "QUADRANT", reason = before + " -> " + Quadrant }); }
    }
    public float Acceptance
    {
        get
        {
            RefreshQuadrant(); var c = ChatSocialConfig.Current;
            double value = Quadrant switch
            {
                InvestorQuadrant.Loyal => c.Get("loyalAcceptanceBase") + FormulaTrust / c.Get("loyalAcceptanceDivisor"),
                InvestorQuadrant.Ambivalent => c.Get("ambivalentAcceptanceBase") + FormulaTrust / c.Get("ambivalentAcceptanceDivisor"),
                InvestorQuadrant.Hostile => 0, _ => FormulaTrust / c.Get("indifferentAcceptanceDivisor")
            };
            return (float)Math.Max(0, Math.Min(1, value));
        }
    }
    public double LeadingAmountScale
    {
        get
        {
            RefreshQuadrant(); var c = ChatSocialConfig.Current;
            return Quadrant switch { InvestorQuadrant.Loyal => c.Get("loyalAmountBase") + FormulaTrust / c.Get("loyalAmountDivisor"),
                InvestorQuadrant.Ambivalent => c.Get("ambivalentAmount"), InvestorQuadrant.Hostile => 0, _ => c.Get("indifferentAmount") };
        }
    }
    public decimal LeadingTarget(decimal buy, decimal target, double roll)
    {
        RefreshQuadrant(); var c = ChatSocialConfig.Current;
        double scale = Quadrant == InvestorQuadrant.Ambivalent ? c.Get("ambivalentTargetMin") + Math.Max(0, Math.Min(1, roll)) * (c.Get("ambivalentTargetMax") - c.Get("ambivalentTargetMin")) : 1;
        return buy + (target - buy) * (decimal)scale;
    }
    public double LeadingStopScale => Quadrant == InvestorQuadrant.Ambivalent ? ChatSocialConfig.Current.Get("ambivalentStopScale") : 1;
    public bool TryFomo(string symbol, double gameMinute, double roll)
    {
        var c = ChatSocialConfig.Current;
        if (string.IsNullOrEmpty(symbol) || gameMinute < FomoUntil || gameMinute < FomoCooldownUntil)
        { return false; }
        double chance = Greed / 100d * (.5 + Traits.Suggestibility / 100d) * (1 - Fear / c.Get("fomoFearDivisor"));
        if (roll >= Math.Max(0, Math.Min(1, chance))) { return false; }
        FomoSymbol = symbol; FomoUntil = gameMinute + c.Get("fomoDurationMinutes"); FomoCooldownUntil = FomoUntil + c.Get("fomoCooldownMinutes");
        ChatSocialAudit.Journal.Event(new SocialEvent { time = gameMinute, actor = SocialId, kind = "FOMO_START", reason = symbol, value = FomoUntil });
        return true;
    }
    public void RememberInvestment(InvestorExperience experience, bool lead)
    {
        if (experience.AssetBefore <= 0) { return; }
        decimal gain = experience.Proceeds - experience.Cost;
        var c = ChatSocialConfig.Current;
        if (Math.Abs(gain) / experience.AssetBefore * 100m < (decimal)c.Get(lead ? "memoryLeadAssetPct" : "memorySelfAssetPct")) { return; }
        Remember(new SocialMemory { type = lead ? gain < 0 ? SocialMemoryKind.LOSS_BY_LEAD : SocialMemoryKind.GAIN_BY_LEAD : gain < 0 ? SocialMemoryKind.BIG_SELF_LOSS : SocialMemoryKind.BIG_SELF_GAIN,
            eventId = experience.EventId, actorId = lead ? -1 : SocialId, targetId = SocialId, symbol = experience.Symbol,
            amount = InvestorSociety.Money(gain), pct = InvestorSociety.Percent(experience.ReturnPercent), at = ChatSocialAudit.GameMinute,
            salience = c.Int(lead ? gain < 0 ? "memoryLossSalience" : "memoryGainSalience" : "memorySelfSalience") });
    }
}

public sealed partial class InvestorSociety
{
    public Func<Stock, double> SocialRecentReturn;
    public event Action<TradeBot, SocialMessage> SocialSpeechAction;
    public void ObserveFomoPrice(TradeBot bot, Stock stock, double risePercent)
    {
        if (stock == null || bot.GetOwnedVolume(stock) > 0 || risePercent < Config.Get("fomoRisePct")) { return; }
        ActivateFomo(bot, stock.symbol);
    }
    private void ActivateFomo(TradeBot bot, string symbol)
    {
        var mind = bot.Social.Psychology;
        if (mind.TryFomo(symbol, SocialTime.GameMinute, mind.SocialRng.Next()))
        { Offer(bot, mind.Room, SocialMotive.EMOTION_THRESHOLD, new SocialFacts { symbol = symbol, notice = "FOMO" }, SocialAct.REACT); }
    }
    partial void ConsiderSocialFomo(TradeBot bot, SocialMessage message) { ActivateFomo(bot, message.facts.symbol); }
    private void TickSocialAutonomy(TradeBot bot)
    {
        var mind = bot.Social.Psychology; mind.RefreshQuadrant();
        if (mind.LastRelationshipSpeech < 0) { mind.LastRelationshipSpeech = SocialTime.GameMinute; }
        if (SocialTime.GameMinute - mind.LastRelationshipSpeech >= Config.Get("relationshipSpeechMinutes"))
        {
            mind.LastRelationshipSpeech = SocialTime.GameMinute;
            if (mind.ActiveAt((float)SocialTime.Seconds) && mind.Latest.HasValue && mind.Latest.Value.CommandId > 0 && mind.Quadrant != InvestorQuadrant.Indifferent)
            {
                int speechRoom = mind.Room;
                if (mind.Quadrant != InvestorQuadrant.Ambivalent && mind.SocialRng.Next() < Config.Get("relationshipPublicChance")) { speechRoom = 0; }
                if (mind.Quadrant != InvestorQuadrant.Ambivalent || speechRoom > 0)
                { Offer(bot, speechRoom, SocialMotive.SELF_EVENT, LeadingExperienceFacts(mind.Latest.Value), target: -1, cause: mind.Latest.Value.EventId); }
            }
        }
        if (mind.LastFomoObservation < 0 || SocialTime.GameMinute - mind.LastFomoObservation >= Config.Get("fomoObservationMinutes"))
        {
            mind.LastFomoObservation = SocialTime.GameMinute;
            if (SocialRecentReturn != null && mind.ActiveAt((float)SocialTime.Seconds))
            {
                var stock = ChooseSocialTopic?.Invoke(bot);
                if (stock != null) { ObserveFomoPrice(bot, stock, SocialRecentReturn(stock)); }
            }
        }
        if (mind.LastQuadrantReview < 0) { mind.LastQuadrantReview = SocialTime.GameMinute; }
        double interval = mind.Quadrant == InvestorQuadrant.Hostile ? 60 : 1440;
        if (SocialTime.GameMinute - mind.LastQuadrantReview < interval) { return; }
        mind.LastQuadrantReview = SocialTime.GameMinute;
        if (mind.Room < 1 || mind.Room > PlayerRoomCount || mind.ExitAt >= 0 || mind.ActiveThread != 0) { return; }
        double chance = mind.Quadrant == InvestorQuadrant.Hostile ? mind.FormulaBetrayal / Config.Get("hostileLeaveDivisor") :
            mind.Quadrant == InvestorQuadrant.Indifferent ? Config.Get("indifferentDailyLeave") : 0;
        if (mind.SocialRng.Next() >= chance) { return; }
        int room = mind.Room;
        SocialDeparted?.Invoke(bot, room, mind.QuadrantLabel + " 관계로 관찰방 이동"); GetRoom(room).Members.Remove(mind.SocialId); mind.Room = 0; mind.Intentions.Clear();
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, room = room, actor = mind.SocialId, kind = "RELATIONSHIP_DEPARTURE", reason = mind.Quadrant.ToString() });
    }
    public SocialFacts LeadingExperienceFacts(InvestorExperience experience)
    {
        return new SocialFacts { symbol = experience.Symbol, stockName = FindSocialStock(experience.Symbol)?.stockName ?? experience.Symbol,
            amount = Money(experience.Proceeds - experience.Cost), pct = Percent(experience.ReturnPercent), directLead = true,
            eventId = experience.EventId, at = SocialTime.GameMinute, profitable = experience.ReturnPercent > 0, losing = experience.ReturnPercent < 0, profitKind = "실현" };
    }
    private void AdjustQuadrantWeight(TradeBot bot, SocialIntent intent, SocialAct act, ref double weight)
    {
        var mind = bot.Social.Psychology; mind.RefreshQuadrant();
        bool playerComment = act == SocialAct.PRAISE_PLAYER || act == SocialAct.WARN_PLAYER || intent.targetId == -1 && (act == SocialAct.DOUBT || act == SocialAct.MOCK);
        if (intent.facts?.notice == "KICK_WARNING" || intent.facts?.notice == "LEFT_WARNING") { return; }
        if (!playerComment) { return; }
        if (mind.Quadrant == InvestorQuadrant.Indifferent || mind.Quadrant == InvestorQuadrant.Ambivalent && intent.roomId == 0)
        { weight = 0; return; }
        if (mind.Quadrant == InvestorQuadrant.Loyal && act == SocialAct.WARN_PLAYER || mind.Quadrant == InvestorQuadrant.Hostile && act == SocialAct.PRAISE_PLAYER ||
            mind.Quadrant == InvestorQuadrant.Loyal && intent.targetId == -1 && (act == SocialAct.DOUBT || act == SocialAct.MOCK) ||
            mind.Quadrant == InvestorQuadrant.Ambivalent && (act == SocialAct.PRAISE_PLAYER || act == SocialAct.WARN_PLAYER))
        { weight = 0; }
        if (mind.Quadrant == InvestorQuadrant.Ambivalent && intent.targetId == -1 && (act == SocialAct.DOUBT || act == SocialAct.MOCK))
        { weight *= Config.Get("ambivalentPlayerDoubtScale"); }
    }
}
