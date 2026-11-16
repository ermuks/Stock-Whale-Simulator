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
