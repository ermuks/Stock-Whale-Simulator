using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static void ModerationScenarios(int stage)
    {
        string folder=Path.Combine(root,"docs/chat-social-results/stage-"+stage);
        var control=SocialReplay(73129,stage);
        var severe=control.Journal.Events.First(e=>e.kind=="ANGER_STAGE" && e.value==3 && e.room==1);
        bool acted=false;
        var moderated=SocialReplay(73129,stage,afterStep:(r,step)=>
        {
            if(acted || r.Society.SocialTime.GameMinute<severe.time) { return; }
            acted=true;
            Check(r.Society.Moderate(1,severe.actor,SocialModeration.Kick,out _),"S1 first fighter kick accepted");
            Check(r.Society.Moderate(1,severe.target,SocialModeration.Kick,out _),"S1 second fighter kick accepted");
        });
        int ArgumentCount(ReplayResult r)=>r.Journal.Events.Count(e=>e.room==1 && e.kind=="ARGUMENT" && e.time>severe.time && e.time<=severe.time+180);
        int before=ArgumentCount(control),after=ArgumentCount(moderated);
        bool reaction=moderated.Messages.Any(m=>m.facts.notice=="DISCIPLINE" && m.at>=severe.time && m.at<severe.time+30);
        Check(after<=before*.5 && reaction,$"S1 next 3h arguments control={before}, both kicked={after}, discipline reaction={reaction}");
        Check(moderated.AvailabilityErrors==0 && moderated.ConcurrencyErrors==0,"moderation T10/T11 remain valid");
        File.WriteAllLines(Path.Combine(folder,"S1-both-kicked.jsonl"),moderated.Messages.Where(m=>m.at>=severe.time && m.at<=severe.time+180).Select(JsonUtility.ToJson).Concat(moderated.Journal.Events.Where(e=>e.time>=severe.time && e.time<=severe.time+180).Select(JsonUtility.ToJson)));

        double initialChill=0;
        var arbitrary=SocialReplay(73129,stage,afterStep:(r,step)=>
        {
            if(step!=0) { return; }
            foreach(int id in Enumerable.Range(1,5)) { r.Society.Moderate(1,id,SocialModeration.Kick,out _); }
            initialChill=r.Society.SocialRoom(1).chill;
        });
        int SpeechCount(ReplayResult r)=>r.Messages.Count(m=>m.roomId==1 && m.at>540 && m.at<=720);
        int ordinaryCount=SpeechCount(control),quietCount=SpeechCount(arbitrary);
        bool chillReaction=arbitrary.Messages.Any(m=>m.facts.notice=="CHILL");
        Check(initialChill>30 && quietCount<=ordinaryCount*.7 && chillReaction,$"S3 chill={initialChill}, next 3h messages control={ordinaryCount}, five arbitrary kicks={quietCount}, reaction={chillReaction}");
        File.WriteAllLines(Path.Combine(folder,"S3-arbitrary-kicks.jsonl"),arbitrary.Messages.Where(m=>m.at<=720).Select(JsonUtility.ToJson).Concat(arbitrary.Journal.Events.Where(e=>e.time<=720).Select(JsonUtility.ToJson)));

        var fixture=SocialReplay(73129,stage,steps:1);var society=fixture.Society;var bot=society.SocialBots[1];
        bot.Social.Psychology.SocialDelta("betrayal",60,"test prior direct loss",1);
        double chillBefore=society.SocialRoom(1).chill;
        Check(society.Moderate(1,1,SocialModeration.Kick,out _) && society.SocialRoom(1).chill==chillBefore,"S5 betrayal >=60 kick adds no chill");
        Check(society.SocialRoom(1).banned.Contains(1) && bot.Social.Psychology.Room==0 && !society.Present(bot,1) && society.Present(bot,0),"kick bans only this room, keeps public market presence");
        Check(bot.Social.Psychology.Memories.Any(m=>m.type==SocialMemoryKind.KICKED) && bot.Social.Psychology.Betrayal==75,"kick is direct betrayal +15 and durable memory");
        Check(!society.Moderate(0,2,SocialModeration.Warn,out _) && !society.Moderate(4,2,SocialModeration.Kick,out _),"moderation rejected in public/NPC rooms");
        var muted=society.SocialBots[2];double angerBefore=muted.Social.Psychology.MaximumAnger(out _);
        Check(society.Moderate(1,2,SocialModeration.Mute,out _) && !society.Present(muted,1) && society.Present(muted,0) && muted.Social.Psychology.MaximumAnger(out _)==angerBefore,"mute only prevents this room's speech; no artificial anger decay");
        society.SocialAdvance(1,571,72,20);
        Check(society.Present(muted,1),"mute expires on game minutes");
        var warningFixture=SocialReplay(73129,stage,steps:1);
        var warningSociety=warningFixture.Society;var warnedBot=warningSociety.SocialBots[2];
        warningSociety.ChangeRelation(warnedBot,3,"anger",90,"test prior conflict",1,1,false);
        warningSociety.Moderate(1,2,SocialModeration.Warn,out _);double firstWarning=warnedBot.Social.Psychology.Relation(3).anger;
        warningSociety.Moderate(1,2,SocialModeration.Warn,out _);double secondWarning=warnedBot.Social.Psychology.Relation(3).anger;
        Check(firstWarning==75 && secondWarning>=67 && secondWarning<=68,"repeated warning within 10 game minutes halves anger relief");

        int unmoderatedFour=0,warnedFour=0,observed=0,pairedSeeds=0;
        var summary=new System.Collections.Generic.List<string>();
        for(int seed=73129;seed<73149;seed++)
        {
            var plain=seed==73129?control:SocialReplay(seed,stage);
            pairedSeeds++;
            var stage2=plain.Journal.Events.FirstOrDefault(e=>e.kind=="ANGER_STAGE" && e.value==2 && e.room==1);
            if(stage2==null) { summary.Add($"seed={seed}: no stage2 trigger in either identical no-action arm; control=False, warning=False"); continue; }
            observed++;bool warned=false;
            var warning=SocialReplay(seed,stage,afterStep:(r,step)=>
            {
                if(warned || r.Society.SocialTime.GameMinute<stage2.time) { return; }
                warned=true;r.Society.Moderate(1,stage2.actor,SocialModeration.Warn,out _);
            });
            bool ReachesFour(ReplayResult r)=>r.Journal.Events.Any(e=>e.kind=="ANGER_STAGE" && e.value==4 && e.room==1 && e.time>=stage2.time && e.time<=stage2.time+180 &&
                (e.actor==stage2.actor && e.target==stage2.target || e.actor==stage2.target && e.target==stage2.actor));
            bool untreated=ReachesFour(plain),treated=ReachesFour(warning);
            if(untreated) { unmoderatedFour++; } if(treated) { warnedFour++; }
            summary.Add($"seed={seed} actor={stage2.actor} opponent={stage2.target} at={stage2.time:F2} next3h_stage4: control={untreated}, warning={treated}");
        }
        Check(pairedSeeds==20 && observed>0 && warnedFour<unmoderatedFour,$"S4 {pairedSeeds} fixed seeds ({observed} stage2 triggers): next3h stage4 control={unmoderatedFour}/{pairedSeeds}, warned={warnedFour}/{pairedSeeds}");
        File.WriteAllLines(Path.Combine(folder,"S4-warn-vs-control.txt"),summary);
    }
}
