using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static void WatchScenarios()
    {
        var fixture = SocialReplay(73129,8,steps:1); var society = fixture.Society;
        var stock = StockManager.Instance.stocks[0];
        var a = society.SocialBots[1]; var b = society.SocialBots[2]; var c = society.SocialBots[3];
        foreach (var bot in new[] { a,b,c }) { bot.Social.Psychology.Intentions.Clear(); }
        society.SocialTime.Set(1,540,1); stock.currentPrice = 1000;
        Check(!society.ObserveWatchedPrice(a,stock),"watch first sight establishes a real baseline without inventing a past price");
        stock.currentPrice = 1300; society.ObserveWatchedPrice(b,stock);
        stock.currentPrice = 1000; society.ObserveWatchedPrice(c,stock);
        society.SocialTime.Set(1,545,2); stock.currentPrice = 1299;
        Check(!society.ObserveWatchedPrice(c,stock),"29.9 percent rise does not trigger the thirty-percent surprise");
        stock.currentPrice = 1300;
        Check(society.ObserveWatchedPrice(a,stock) && !society.ObserveWatchedPrice(b,stock),"same price surprises only the bot whose own previous observation was thirty percent lower");
        var intent = a.Social.Psychology.Intentions.Single(i=>i.facts?.notice=="WATCH_RISE");
        Check(intent.forcedAct == SocialAct.REACT && intent.facts.observationPrice == "1,000" && intent.facts.observedPrice == "1,300" && intent.facts.observationPercent == "+30.0" && intent.facts.observedAt-intent.facts.observationAt==5,"surprise retains its event ID, elapsed game minutes and both observed prices");
        SocialMessage published = null; society.SocialMessagePublished += message => { if(message.senderId == a.Social.Id && message.facts?.notice=="WATCH_RISE") { published=message; } };
        stock.currentPrice = 1400;
        typeof(InvestorSociety).GetMethod("GenerateSocialMessage",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(society,new object[] { a,intent });
        Check(published != null && published.templateKey.StartsWith("watch.RISE.") && published.facts.price == "1,300" && !published.text.Contains("{"),"existing chat pipeline publishes a natural reaction using the observation snapshot, not an invented profit or a later price");
        SocialMessage reply = null; society.SocialMessagePublished += message => { if(message.senderId == b.Social.Id) { reply=message; } };
        var echoed = new SocialIntent { roomId=1,motive=SocialMotive.WITNESSED_EVENT,forcedAct=SocialAct.REACT,facts=intent.facts };
        typeof(InvestorSociety).GetMethod("GenerateSocialMessage",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(society,new object[] { b,echoed });
        Check(reply != null && reply.facts.notice == null && reply.facts.observationPrice == null && !reply.templateKey.StartsWith("watch."),"hearing another bot's surprise cannot impersonate that bot's personal observation");
        society.SocialTime.Set(1,546,3); stock.currentPrice = 1300;
        Check(!society.ObserveWatchedPrice(a,stock),"stationary price does not repeat the same surprise");
        society.SocialTime.Set(1,547,4); stock.currentPrice = 2000;
        Check(!society.ObserveWatchedPrice(a,stock),"cooldown suppresses another shock from the same bot");
        society.SocialTime.Set(1,560,5); stock.currentPrice = 2600;
        Check(society.ObserveWatchedPrice(a,stock),"fresh thirty-percent move after cooldown permits another reaction");
        society.SocialTime.Set(1,561,6); stock.currentPrice = 1000; society.ObserveWatchedPrice(b,stock);
        society.SocialTime.Set(1,575,7); stock.currentPrice = 700;
        Check(society.ObserveWatchedPrice(b,stock) && b.Social.Psychology.Intentions.Any(i=>i.facts?.notice=="WATCH_FALL"),"thirty-percent fall produces the corresponding alarm instead of a bullish message");
        society.SocialTime.Set(2,540,8); stock.currentPrice = 2000;
        Check(!society.ObserveWatchedPrice(a,stock),"overnight change resets the observation instead of pretending it happened in a few minutes");
        society.SocialTime.Set(2,590,9); stock.currentPrice = 4000;
        Check(!society.ObserveWatchedPrice(a,stock),"long absence resets stale watch baseline");
        var other = new Stock { symbol="NEW",stockName="다른 관심주",currentPrice=10000 };
        Check(!society.ObserveWatchedPrice(a,other),"switching interest stocks never compares different companies' prices");
        var rows = ChatSocialAssets.Templates.templates.Where(t=>t.key.StartsWith("watch.")).ToArray();
        Check(rows.Length==108 && rows.All(t=>t.requires.Single().StartsWith("notice:WATCH_")) && rows.Select(t=>t.key).Distinct().Count()==108,"rise/fall templates cover three personalities and three intensities with six distinct lines each");
    }
}
