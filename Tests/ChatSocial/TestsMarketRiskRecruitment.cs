using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static void MarketRiskRecruitmentScenarios()
    {
        ChatSocialAssets.Reload();
        var cautious = new InvestorPsychology(4101,73129);
        var speculative = new InvestorPsychology(4102,73129);
        var saved = cautious.CaptureSocial(); saved.greed=65; saved.fear=10; cautious.RestoreSocial(saved);
        saved=speculative.CaptureSocial(); saved.greed=85; saved.fear=0; speculative.RestoreSocial(saved);
        Check(cautious.MarketRiskBuyScale("NM",0)==1,"unseen bulletin cannot change a bot's order size");
        Check(cautious.ObserveMarketRisk("NM",1,1,9001,0,.05,true) && cautious.Fear>10 && cautious.MarketRiskBuyScale("NM",0)<1,"cautious holder acknowledges warning with fear and a smaller future buy");
        float fear=cautious.Fear;
        Check(!cautious.ObserveMarketRisk("NM",1,1,9001,1,.05,true) && cautious.Fear==fear,"duplicate warning version cannot accumulate fear");
        Check(cautious.ObserveMarketRisk("NM",2,3,9002,2,.05,true) && cautious.MarketRiskBuyScale("NM",3)==0,"high risk pauses cautious holder's additional buys");
        Check(cautious.MarketRiskBuyScale("NM",25)>0 && cautious.MarketRiskBuyScale("NM",25)<.5 && cautious.MarketRiskBuyScale("OTHER",3)==1,"pause expires to reduced sizing and cannot affect unrelated symbols");
        Check(cautious.MarketRiskBuyScale("NM",125)==1,"missing a later recovery bulletin cannot suppress new buy sizes forever");
        speculative.ObserveMarketRisk("NM",2,3,9002,2,.95,false);
        Check(speculative.MarketRiskBuyScale("NM",3)==1,"speculative greedy bot can knowingly dismiss the same notice");
        cautious.ObserveMarketRisk("NM",3,0,9003,30,.05,true);
        Check(cautious.MarketRiskBuyScale("NM",30)==1,"observed safe downgrade removes the additional buy restriction");
        fear=cautious.Fear; cautious.ObserveMarketRisk("NM",4,3,9004,31,.05,true);
        Check(cautious.Fear==fear,"repeated grade oscillation does not pump already-experienced peak fear");

        var fixture=SocialReplay(73129,8,steps:1); var society=fixture.Society;
        var stock=StockManager.Instance.stocks[0]; var bot=society.SocialBots[1];
        stock.RiskGrade=StockRiskGrade.HighRisk; stock.RiskVersion=1; stock.RiskPending=false;
        Time.time=10; society.SocialTime.Set(1,550,10);
        long notice=society.NotifyMarketRisk(stock);
        Check(notice>0 && society.NotifyMarketRisk(stock)==notice,"production bulletin registration deduplicates stock and version");
        bot.Social.Psychology.ObserveMarketRisk(stock.symbol,1,3,notice,10,.05,true);
        var intent=new SocialIntent {roomId=1,motive=SocialMotive.SELF_EVENT,forcedAct=SocialAct.REACT,
            facts=new SocialFacts {symbol=stock.symbol,notice="MARKET_RISK_PAUSE",riskGrade=3,riskVersion=1,riskObserver=bot.Social.Id,eventId=notice}};
        var generated=typeof(InvestorSociety).GetMethod("GenerateSocialMessage",BindingFlags.Instance|BindingFlags.NonPublic);
        SocialMessage message=null; society.SocialMessagePublished+=row=>message=row;
        generated.Invoke(society,new object[]{bot,intent});
        Check(message!=null && message.templateKey.StartsWith("market-risk.") && message.text.Contains("고위험") && !message.text.Contains("{"),"warning speech uses live grade, bot stance and guarded Korean text");
        message=null; stock.RiskVersion=2; stock.RiskGrade=StockRiskGrade.Safe;
        generated.Invoke(society,new object[]{bot,intent});
        Check(message==null,"typing an obsolete warning is discarded after an exchange-grade change");
        Check(ChatSocialAssets.Templates.templates.Count(t=>t.key.StartsWith("market-risk."))==216,"risk templates cover six stances without leaking into ordinary chatter");

        var day=new MarketDayController(); day.InitializeReplay(73129);
        society=new InvestorSociety(); society.BindSocialBots(day.Bots); InvestorSociety.Instance=society;
        PlayerProgression.Instance=null; Time.time=100; society.SocialTime.Set(1,560,100);
        var first=day.Bots[0]; var unaware=day.Bots[1]; var witness=day.Bots[2]; var banned=day.Bots[3];
        var choose=typeof(InvestorSociety).GetMethod("ChooseRoom",BindingFlags.NonPublic|BindingFlags.Instance);
        Check((int)choose.Invoke(society,new object[]{first})==0 && !society.Join(first,1),"opening player rooms creates no random entry before an invitation");
        society.GetRoom(4).OwnerBotId=99;
        Check((int)choose.Invoke(society,new object[]{first})==4,"existing NPC room remains discoverable without a player invitation");
        society.GetRoom(1).Social.banned.Add(banned.Social.Id);
        Check(!society.Invite(banned.Social.Id,1) && society.InvitedRoomMask==0,"first-invite guarantee cannot bypass a ban or consume its entitlement on failure");
        Check(society.Invite(first.Social.Id,1) && first.Social.Psychology.Room==1 && society.GetRoom(1).Members.Contains(first.Social.Id),"first eligible bot selected by player joins regardless of initial trust probability");
        Check(!society.Join(unaware,1) && !society.CanDiscoverPlayerRoom(unaware,1),"first member does not magically reveal the room to the rest of the population");
        Check(society.IsRoomOpen(1) && !society.IsRoomOpen(2) && !society.IsRoomOpen(3) && !society.Invite(unaware.Social.Id,2) && !society.Join(unaware,3,true),"one player room only: legacy second/third rooms cannot open or accept even a direct invite");
        var profit=new InvestorExperience(990001,51,1,stock.symbol,1000,1300,100,"fixture actual fill");
        first.Social.Psychology.Record(profit,new InvestorSocietySettings());
        var testimony=new SocialMessage {id=990002,senderId=first.Social.Id,roomId=0,act=SocialAct.PRAISE_PLAYER,facts=new SocialFacts{eventId=profit.EventId,directLead=true,profitable=true,symbol=stock.symbol}};
        Check(society.ObserveRoomReferral(witness,first,testimony) && society.CanDiscoverPlayerRoom(witness,1) && !society.CanDiscoverPlayerRoom(unaware,1),"only a witness to a member's actual profitable instruction learns that room");
        testimony.facts.eventId=990099;
        Check(!society.ObserveRoomReferral(unaware,first,testimony),"made-up profitable testimony cannot recruit investors");
        testimony.facts.eventId=profit.EventId; testimony.roomId=1;
        Check(!society.ObserveRoomReferral(unaware,first,testimony),"private testimonials cannot be overheard outside the room");
        saved=witness.Social.Psychology.CaptureSocial();
        var restored=new InvestorPsychology(witness.Social.Id,73129);
        Check(restored.RestoreSocial(saved) && restored.KnownPlayerRooms.ContainsKey(1),"heard-room knowledge survives social snapshot round trip");
        saved.room=2; restored.RestoreSocial(saved);
        Check(restored.Room==0,"old second-room membership restores to public lounge without exposing an extra player room");
        society.SocialTime.Set(1,690,230);
        Check(!society.CanDiscoverPlayerRoom(witness,1) && society.CanDiscoverPlayerRoom(first,1),"stale rumor expires while a personal invitation remains known");
        var progression=new PlayerProgression {CanOperateRooms=true}; string json=progression.Capture();
        society.RestoreInvitedRooms(0);
        Check(progression.Restore(json,progression.WorldId) && society.InvitedRoomMask==1,"progression snapshot preserves used first-invite entitlement independently per room");
        Time.time=1000; first.Social.Psychology.Room=0; society.GetRoom(1).Members.Clear();
        Check(society.GetRoom(1).Social.firstMemberInvited,"emptying a room cannot regenerate the guaranteed first-member invitation");
        InvestorSociety.Instance=null;
    }
}
