using System;
using System.IO;
using UnityEngine;

public sealed partial class InvestorSociety
{
    private bool socialWired;
    private SocialLogSink socialLog;
    private float nextSocialLogFlush;
    private void PrepareSocialRuntime()
    {
        BindSocialBots(day.Bots);
        if (!socialWired)
        {
            ConfigureLiveConversation(day.ConversationFocus);
            ChooseSocialTopic = bot => ChooseRoomTopic(bot, bot.Social.Psychology.Room, bot.Social.Psychology.SocialRng.Next());
            ChooseWatchedStock = ChoosePersonalWatch;
            RefreshOfferedFacts = RefreshNpcFacts;
            SocialRecentReturn = stock => (double)day.ConversationFocus.Read(stock, SessionClock.Time).ReturnPercent;
            WitnessChance = bot => Config.Get("witnessProbability") * (Config.Get("witnessActivityBase") + bot.Social.Psychology.Activity * Config.Get("witnessActivityScale"));
            SocialMessagePublished += DeliverSocialMessage;
            SocialDeparted += OnSocialDeparture;
            CanModerateRoom = room => IsRoomOpen(room);
            SocialSystemNotice += DeliverSocialSystemNotice;
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "SocialLogs"); Directory.CreateDirectory(directory);
                socialLog = new SocialLogSink(new StreamWriter(Path.Combine(directory, "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl")),
                    message => Debug.LogWarning("Social log disabled; in-memory audit continues: " + message));
                ChatSocialAudit.Journal.Changed += WriteSocialChange; ChatSocialAudit.Journal.Happened += WriteSocialEvent;
            }
            catch (IOException ex) { Debug.LogWarning("Social log file unavailable: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { Debug.LogWarning("Social log file unavailable: " + ex.Message); }
            socialWired = true;
        }
        SocialTime.Set(day.Day, day.GameMinute, SessionClock.Time);
        TickHype();
        if (socialLog != null && Time.unscaledTime >= nextSocialLogFlush)
        { socialLog.Flush(); nextSocialLogFlush = Time.unscaledTime + (float)Config.Get("logFlushSeconds"); }
    }
    private void WriteSocialChange(SocialChange change) { socialLog?.Write(JsonUtility.ToJson(change)); }
    private Stock ChoosePersonalWatch(TradeBot bot)
    {
        if (bot.IsSingleStockTrader) { return bot.SpecializedStock; }
        var stocks = StockManager.Instance?.stocks;
        if (stocks == null) { return null; }
        Stock chosen = null;
        double total = 0;
        foreach (var stock in stocks)
        {
            if (!stock.AcceptsOrders) { continue; }
            double weight = 1 + Math.Min(3d, Math.Max(0d, bot.GetPreference(stock))) + (bot.GetOwnedVolume(stock) > 0m ? 3d : 0d);
            total += weight;
            if (bot.Social.Psychology.SocialRng.Next() * total < weight) { chosen = stock; }
        }
        return chosen;
    }
    private void WriteSocialEvent(SocialEvent record) { socialLog?.Write(JsonUtility.ToJson(record)); }
    private void DisposeSocialRuntime()
    {
        ChatSocialAudit.Journal.Changed -= WriteSocialChange; ChatSocialAudit.Journal.Happened -= WriteSocialEvent;
        SocialMessagePublished -= DeliverSocialMessage; socialLog?.Dispose(); socialLog = null;
        SocialDeparted -= OnSocialDeparture;
        SocialSystemNotice -= DeliverSocialSystemNotice;
        socialWired = false;
    }
    private void DeliverSocialSystemNotice(int roomId, string text, long id, ChatRoomNotice notice)
    {
        if (day == null) { return; }
        if (roomId == 0) { day.PublishRoomNotice(text, notice, id); return; }
        var room = GetRoom(roomId);
        if (room == null) { return; }
        room.Add(new BotChatMessage(room.Version + 1, null, day.Day, day.GameMinute, text, "SYSTEM.MODERATION", ChatEvent.Agreement,
            new InvestorMessageContext(id, roomId, 0, 1, 0, false), notice: notice));
        LastAction = text;
    }
    private void OnSocialDeparture(TradeBot bot, int room, string reason, ChatRoomNotice notice) { Leave(bot, reason, notice); }
    private void DeliverSocialMessage(SocialMessage message)
    {
        if (!SocialBots.TryGetValue(message.senderId, out var bot))
        { return; }
        if (SplitSocialDelivery(bot, message)) { return; }
        var context = new InvestorMessageContext(message.facts?.eventId ?? 0, message.roomId,
            message.act == SocialAct.PRAISE_PLAYER ? 1 : message.act == SocialAct.WARN_PLAYER ? -1 : 0,
            bot.Social.Confidence, bot.Social.Psychology.Trust, message.facts != null && message.facts.directLead,
            message.replyToId, target: message.targetId);
        if (message.roomId == 0) { day.PublishSocialParticipant(bot, message, context); }
        else
        {
            var room = GetRoom(message.roomId);
            room?.Add(new BotChatMessage(room.Version + 1, bot, day.Day, day.GameMinute, message.text, message.templateKey, ChatEvent.Agreement, context, message));
        }
    }
    public void ObserveSocialFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller, Order resting)
    {
        if (ReferenceEquals(buyer, seller) || volume <= 0 || resting == null)
        { return; }
        ObserveHypeFill(stock, price, volume, buyer, seller);
        double at = day == null ? 0 : (day.Day - 1) * 1440d + day.GameMinute;
        if (buyer is TradeBot buying && buying.Social != null)
        {
            buying.Social.Psychology.RecordSocialFill(new SocialTradeFact { symbol = stock.symbol, bought = true, price = price, quantity = volume, cost = price, at = at });
            Offer(buying, buying.Social.Psychology.Room, SocialMotive.SELF_EVENT, CaptureFacts(buying, stock));
        }
        if (seller is TradeBot selling && selling.Social != null)
        {
            decimal basis = ReferenceEquals(resting.owner, seller) ? resting.costBasis : selling.SubmittedSellCost;
            selling.Social.Psychology.RecordSocialFill(new SocialTradeFact { symbol = stock.symbol, bought = false, price = price, quantity = volume, cost = basis, at = at });
            bool leading = Orders?.Get(selling) is LeadingRoomOrders.Plan plan && plan.Command.Stock == stock && !plan.Reported && plan.Remaining > 0;
            if (!leading && basis > 0)
            {
                decimal profit = (price - basis) * volume;
                selling.Social.Psychology.RememberInvestment(new InvestorExperience(NextEventId(), 0, 0, stock.symbol, basis * volume, price * volume, SessionClock.Time,
                    assetBefore: Math.Max(0, selling.Equity() - profit)), false);
            }
            Offer(selling, selling.Social.Psychology.Room, SocialMotive.SELF_EVENT, CaptureFacts(selling, stock));
        }
    }
    public void OfferExistingSpeech(TradeBot bot, int room, InvestorMessageContext context, ChatEvent kind)
    {
        var facts = CaptureFacts(bot);
        if (context.DirectExperience && bot.Social.Psychology.Latest.HasValue)
        {
            var experience = bot.Social.Psychology.Latest.Value;
            if (experience.EventId == context.EventId)
            {
                facts = new SocialFacts { symbol = experience.Symbol, stockName = experience.Symbol, amount = Money(experience.Proceeds - experience.Cost),
                    pct = Percent(experience.ReturnPercent), directLead = true, eventId = experience.EventId, at = SocialTime.GameMinute,
                    profitable = experience.ReturnPercent > 0, losing = experience.ReturnPercent < 0, profitKind = "실현" };
            }
        }
        Offer(bot, room, facts.directLead ? SocialMotive.SELF_EVENT : SocialMotive.WITNESSED_EVENT, facts);
    }
    public void OfferNpcObservation(TradeBot leader)
    {
        var facts = RefreshNpcFacts(leader, null);
        if (facts != null) { Offer(leader, NpcLeadingRoom.RoomId, SocialMotive.SELF_EVENT, facts, SocialAct.REACT, cause: facts.eventId); }
    }
    private SocialFacts RefreshNpcFacts(TradeBot leader, SocialFacts previous)
    {
        var npc = NpcLeadingRoom.Instance; var signal = npc?.Current;
        if (npc == null || npc.Leader != leader || signal == null || signal.ExpiresAt <= SessionClock.Time)
        { return null; }
        return new SocialFacts { signal = true, eventId = signal.Id, symbol = signal.Stock.symbol, stockName = signal.Stock.stockName, at = SocialTime.GameMinute,
            bidStatus = signal.Buy > 0 ? Money(signal.Buy) + (signal.RestingBuy ? "원 매수 주문 대기" : "원 최근 매수 체결") : "매수 주문 없음",
            askStatus = signal.Sell > 0 ? Money(signal.Sell) + "원 매도 주문 대기" : "매도 주문 없음", signalResult = Money(npc.RealizedProfit) };
    }
}
