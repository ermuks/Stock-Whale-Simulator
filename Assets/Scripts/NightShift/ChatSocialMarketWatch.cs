using System;

public sealed partial class InvestorPsychology
{
    internal Stock WatchedStock;
    internal decimal WatchedPrice;
    internal double WatchedAt = -1, NextWatchAt, WatchReactionUntil, WatchSelectedAt;
}

public sealed partial class InvestorSociety
{
    public Func<TradeBot, Stock> ChooseWatchedStock;

    private void TickMarketWatch(TradeBot bot)
    {
        var mind = bot.Social.Psychology;
        double now = SocialTime.GameMinute;
        if (!mind.ActiveAt((float)SocialTime.Seconds) || now < mind.NextWatchAt || ChooseWatchedStock == null)
        { return; }
        mind.NextWatchAt = now + Config.Get("watchObservationMinMinutes") + mind.SocialRng.Next() * (Config.Get("watchObservationMaxMinutes") - Config.Get("watchObservationMinMinutes"));
        var stock = mind.WatchedStock;
        if (stock == null || !stock.AcceptsOrders || now - mind.WatchSelectedAt >= Config.Get("watchRetargetMinutes"))
        { stock = ChooseWatchedStock(bot); mind.WatchSelectedAt = now; }
        ObserveWatchedPrice(bot, stock);
    }

    /// <summary>Compare only prices this bot actually observed. Initial/overnight/stale samples establish a new baseline.</summary>
    public bool ObserveWatchedPrice(TradeBot bot, Stock stock)
    {
        if (bot?.Social == null || stock == null || !stock.AcceptsOrders || stock.currentPrice <= 0m)
        { return false; }
        var mind = bot.Social.Psychology;
        double now = SocialTime.GameMinute, beforeAt = mind.WatchedAt;
        decimal before = mind.WatchedPrice, current = stock.currentPrice;
        bool comparable = mind.WatchedStock == stock && before > 0m && beforeAt >= 0 && now > beforeAt &&
            Math.Floor(beforeAt / 1440d) == Math.Floor(now / 1440d) && now - beforeAt <= Config.Get("watchMaximumGapMinutes");
        mind.WatchedStock = stock; mind.WatchedPrice = current; mind.WatchedAt = now;
        if (!comparable || now < mind.WatchReactionUntil)
        { return false; }
        decimal move = (current / before - 1m) * 100m;
        if (Math.Abs(move) < (decimal)Config.Get("watchMovePercent"))
        { return false; }
        // 본 것과 지금 값의 차이만으로 놀라지 않는다. 그 움직임이 지금 상황에서 얘깃거리일 때만 말을 꺼낸다:
        // 괴물 급등 중에 몇 % 내려온 것, 투매 중에 몇 % 튄 것, 아무 일 없는 종목의 출렁임은 지나친다(방금 본 값은 새 기준으로 남는다).
        var situation = SituationOf(stock);
        if (situation.valid && !ChatSituationRules.PriceEventMatters(situation.kind, move > 0m))
        { return false; }
        var facts = CaptureFacts(bot, stock);
        facts.notice = move > 0m ? "WATCH_RISE" : "WATCH_FALL";
        facts.observationPrice = Money(before); facts.observedPrice = Money(current); facts.observationPercent = Percent(move);
        facts.observationBotId = mind.SocialId;
        facts.observationAt = beforeAt; facts.observedAt = now;
        facts.eventId = ChatSocialIds.Next();
        mind.WatchReactionUntil = now + Config.Get("watchReactionCooldownMinutes");
        Offer(bot, mind.Room, SocialMotive.SELF_EVENT, facts, SocialAct.REACT, cause: facts.eventId);
        ChatSocialAudit.Journal.Event(new SocialEvent { id = facts.eventId, time = now, room = mind.Room, actor = mind.SocialId,
            kind = "WATCH_PRICE_SHOCK", value = (double)move, reason = stock.symbol + " " + Money(before) + " -> " + Money(current) });
        return true;
    }
    private static void PreserveWatchObservation(TradeBot bot, SocialIntent intent, SocialFacts current)
    {
        var offered = intent.facts;
        if (offered == null || offered.notice != "WATCH_RISE" && offered.notice != "WATCH_FALL")
        { return; }
        if (offered.observationBotId != bot.Social.Id || intent.replyToId != 0)
        {
            // A reply can discuss the topic, but must not claim another bot's personal price observation.
            current.notice = null;
            return;
        }
        current.observationBotId = offered.observationBotId;
        current.observationPrice = offered.observationPrice; current.observedPrice = offered.observedPrice; current.observationPercent = offered.observationPercent;
        current.observationAt = offered.observationAt; current.observedAt = offered.observedAt;
        current.price = offered.observedPrice;
    }
}
