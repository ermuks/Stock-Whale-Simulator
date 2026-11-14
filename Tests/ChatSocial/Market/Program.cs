using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
public static class MarketTests
{
    public static int Main(string[] args)
    {
        Resources.Root=args[0];int seed=int.Parse(args[1]);UnityEngine.Random.InitState(seed);
        var journal=new SocialJournal(100000);ChatSocialAudit.Begin(journal);ChatSocialIds.Reset();
        StockManager.Instance=new StockManager();MarketDayController.Instance=new MarketDayController {seed=seed};
        for(int i=0;i<8;i++) { StockManager.Instance.stocks.Add(new Stock("S"+i,"시장"+i,500+i*1000,10000000)); }
        var interest=new GameObject("Interest").AddComponent<StockInterestManager>();GameObject.Invoke(interest,"Start");
        for(int i=0;i<20;i++)
        {
            var bot=new GameObject("Investor"+i).AddComponent<RandomBot>();GameObject.Invoke(bot,"Start");
            bot.cash=1000000;foreach(var stock in StockManager.Instance.stocks) { bot.GrantShares(stock,100,stock.currentPrice); }
        }
        var day=MarketDayController.Instance;var society=new InvestorSociety {day=day};InvestorSociety.Instance=society;society.BindSocialBots(day.Bots);
        foreach(var bot in day.Bots) { bot.Social.Psychology.Room=1;society.GetRoom(1).Members.Add(bot.Social.Id); }
        society.ChooseSocialTopic=bot=>day.ConversationFocus.Choose(bot.Social.Psychology.SocialRng.Next(),Time.time);
        society.SocialRecentReturn=stock=>(double)day.ConversationFocus.Read(stock,Time.time).ReturnPercent;
        int fills=0;var messages=new List<SocialMessage>();society.SocialMessagePublished+=messages.Add;
        var tape=new List<string>();
        foreach(var stock in StockManager.Instance.stocks)
        {
            day.ConversationFocus.Register(stock);
            stock.OnMatchedTrade+=(s,p,q,b,a,o)=> { fills++;society.ObserveSocialFill(s,p,q,b,a,o);day.ConversationFocus.Record(s,p,q,Time.time);tape.Add($"{ChatSocialAudit.GameMinute:F2}|{s.symbol}|{p}|{q}"); };
        }
        decimal startingCash=day.Bots.Sum(b=>b.cash);var issued=StockManager.Instance.stocks.ToDictionary(s=>s,s=>day.Bots.Sum(b=>b.GetOwnedVolume(s)));
        int days=int.Parse(args[2]);
        for(int step=0;step<3324*4*days;step++)
        {
            Time.time=Time.unscaledTime=step*.25f;double gameMinute=540+Time.time*390d/900d;
            day.Day=(int)(gameMinute/1440)+1;day.GameMinute=(int)(gameMinute%1440);ChatSocialAudit.GameMinute=gameMinute;
            society.SocialTime.Set(day.Day,gameMinute%1440,Time.time);
            foreach(var bot in day.Bots) { bot.Pump(); }
            GameObject.Invoke(interest,"Update");
            if(step%4==0) { society.SocialAdvance(day.Day,gameMinute%1440,Time.time,20); }
        }
        var folder=Path.Combine(args[0],"docs/chat-social-results/stage-6/market");Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder,$"seed-{seed}-messages.jsonl"),messages.Select(JsonUtility.ToJson));
        File.WriteAllLines(Path.Combine(folder,$"seed-{seed}-tape.txt"),tape);
        File.WriteAllLines(Path.Combine(folder,$"seed-{seed}-events.jsonl"),journal.Events.Select(JsonUtility.ToJson));
        var room=messages.Where(m=>m.roomId==1).ToArray();
        var directed=room.Where(m=>m.targetId>0).ToArray();var closed=society.ClosedThreads.Where(t=>t.roomId==1).ToArray();
        int answered=directed.Count(m=>room.Any(r=>r.replyToId==m.id && r.senderId==m.targetId));
        double[] rates={directed.Length==0?0:(double)answered/directed.Length,closed.Length==0?0:closed.Average(t=>t.turnCount),room.Length==0?0:(double)room.Count(m=>m.targetId!=0)/room.Length,
            journal.Events.Count(e=>e.kind=="ARGUMENT" && e.room==1),society.VoluntaryExits,society.VoluntaryExits==0?0:(double)society.SympathyExits/society.VoluntaryExits,
            room.Length==0?0:(double)room.Count(m=>m.memory)/room.Length,society.SocialAttempts==0?0:(double)society.TemplateBlocked/society.SocialAttempts};
        for(int i=0;i<rates.Length;i++) { Console.WriteLine($"R{i+1}={rates[i]:F6}"); }
        File.WriteAllText(Path.Combine(folder,$"seed-{seed}-rates.json"),System.Text.Json.JsonSerializer.Serialize(rates));
        bool accounts=startingCash==day.Bots.Sum(b=>b.cash) && issued.All(pair=>pair.Value==day.Bots.Sum(b=>b.GetOwnedVolume(pair.Key))) && day.Bots.All(b=>b.cash>=0 && b.ReservedCash<=b.cash);
        string report=$"seed={seed} fills={fills} messages={room.Length} memory={room.Count(m=>m.memory)} ratio={(room.Length==0?0:(double)room.Count(m=>m.memory)/room.Length):P3} conserved={accounts}";
        Console.WriteLine(report);File.WriteAllText(Path.Combine(folder,$"seed-{seed}-summary.txt"),report);
        return accounts && fills>0 ? 0:1;
    }
}
