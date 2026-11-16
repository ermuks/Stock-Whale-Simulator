using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static int failed, passed;
    private static string root;
    private static void Check(bool condition,string label)
    { Console.WriteLine((condition?"PASS ":"FAIL ")+label); if(condition) { passed++; } else { failed++; } }
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        root=args[0]; Resources.Root=root;
        int stage=int.Parse(args[1]);
        try
        {
            if(args.Length>2 && args[2]=="moderation") { ModerationScenarios(stage); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="news") { NewsSocietyScenarios(); Console.WriteLine($"News: passed={passed}, failed={failed}"); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="rates") { SocialReplays(stage); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="tune") { SearchTuning(stage); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="decisions") { DecisionScenarios(stage); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="focus") { FocusScenarios(); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="polish") { PolishScenarios(); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="watch") { WatchScenarios(); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="voice") { SpeechScenarios(); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="variety") { VoiceVarietyScenarios(); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="flow") { ConversationFlowScenarios(); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="risk-recruitment") { MarketRiskRecruitmentScenarios(); return failed==0?0:1; }
            if(args.Length>2 && args[2]=="mediation") { MediationScenarios(); return failed==0?0:1; }
            ModelTests(); LegacyReplay();
            if(stage>=2) { SocialReplays(stage); }
            if(stage>=4) { IgnoredFightScenario(stage); }
            if(stage>=5) { ModerationScenarios(stage); }
            if(stage>=6) { DecisionScenarios(stage); }
            if(stage>=7) { FocusScenarios(); }
            if(stage>=8) { PolishScenarios(); }
            if(stage>=8) { WatchScenarios(); }
            if(stage>=8) { SpeechScenarios(); }
            if(stage>=8) { MediationScenarios(); }
            if(stage>=8) { VoiceVarietyScenarios(); ConversationFlowScenarios(); }
            if(stage>=8) { MarketRiskRecruitmentScenarios(); }
            if(stage>=8) { NewsSocietyScenarios(); }
        }
        catch(Exception ex) { Console.WriteLine(ex); failed++; }
        Console.WriteLine($"Stage {stage}: passed={passed}, failed={failed}"); return failed==0?0:1;
    }
    private static void ModelTests()
    {
        var config=ChatSocialConfig.Current;
        Check(config.Int("memoryCapacity")==8,"configuration loaded from production JSON");
        var ids=Enumerable.Range(1,20).ToArray();
        var a=new InvestorPsychology(1,73129); var b=new InvestorPsychology(1,73129);
        a.SeedFriends(ids); b.SeedFriends(ids);
        Check(a.Relations.Count>=2 && a.Relations.Count<=4 && JsonUtility.ToJson(a.CaptureSocial())==JsonUtility.ToJson(b.CaptureSocial()),"seeded traits and directional friends repeat exactly");
        foreach(var relation in a.Relations.Values)
        { Check(relation.to!=1 && relation.affinity>=config.Get("friendAffinityMin") && relation.affinity<=config.Get("friendAffinityMax") && relation.affinity>=-100 && relation.affinity<=100 && relation.anger==0,"friend seed configured bounds and absolute -100..100 range"); }
        for(int i=0;i<10;i++) { a.Remember(new SocialMemory { eventId=i+1,at=i,salience=i,type=SocialMemoryKind.FOUGHT }); }
        Check(a.Memories.Count==8 && a.Memories.Min(m=>m.salience)==2,"memory eviction lowest salience then oldest");
        var old=JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>("{\"version\":1,\"id\":1,\"room\":1,\"greed\":70,\"fear\":40,\"trust\":180,\"betrayal\":130,\"fomo\":80}");
        Check(a.RestoreSocial(old) && a.Trust==180 && a.Betrayal==130 && !a.HasFomo && a.FomoUntil==0,"v1 migration keeps overheat values and discards numeric FOMO");
        Check(a.FormulaTrust==100 && a.FormulaBetrayal==100,"D01 probability inputs cap independently of real values");
        var snapshot=a.CaptureSocial();
        Check(b.RestoreSocial(snapshot) && b.Trust==180 && b.Betrayal==130 && b.SocialRng.State==a.SocialRng.State,"v2 restore preserves state and RNG");
        snapshot.version=99; Check(!b.RestoreSocial(snapshot),"future snapshot rejected without mutation");
        var clock=new SocialClock();clock.Set(2,540,900);var t=clock.Seconds;clock.Set(2,540,900);
        Check(clock.GameMinute==1980 && clock.Seconds==t,"clock preserves paused sample and day transition");
        var journal=new SocialJournal(2);journal.Change(3,1,"trust",0,4,"fill",12);journal.Change(3,1,"trust",4,4,"idle",0);
        Check(journal.Changes.Count==1 && journal.Changes.Peek().causeId==12,"change audit omits unchanged values and retains cause");
        var progression=new PlayerProgression();
        const string oldProgress="{\"version\":1,\"worldId\":\"fixture-world\",\"turnover\":\"100950\",\"realizedProfit\":\"950\",\"roomsOpened\":false}";
        Check(progression.Restore(oldProgress,"fixture-world") && progression.Turnover==100950m && progression.RealizedProfit==950m && !progression.CanOperateRooms,"existing PlayerProgression v1 fixture loads via unchanged Restore");
        Check(!progression.Restore(oldProgress,"other-world") && progression.Turnover==100950m,"existing world mismatch rejection preserved");
    }
    private static List<string> Replay(int seed)
    {
        InvestorSociety.Instance=null; StockManager.Instance=new StockManager();
        var day=new MarketDayController();day.InitializeReplay(seed);
        // Same production ProcessChat/Eligible/event queue; explicit clock makes callback ordering repeatable.
        for(int seconds=0;seconds<3324;seconds++)
        { day.ReplayStep(seconds,540+seconds*(390d/900d)); }
        return day.AllMessages.Select(m=>$"{m.Day}:{m.Minute:D4}|{m.BotId}|{m.TemplateId}|{m.Text}").ToList();
    }
    private static void LegacyReplay()
    {
        var folder=Path.Combine(root,"docs/chat-social-results/stage-1");Directory.CreateDirectory(folder);
        int index=0;
        foreach(int seed in new[]{73129,73130,73131,73132,73133})
        {
            var first=Replay(seed);var second=Replay(seed);
            Check(first.Count>0 && first.SequenceEqual(second),$"T1 legacy production chat seed={seed}, messages={first.Count}");
            if(index++<3) { File.WriteAllLines(Path.Combine(folder,$"seed-{seed}-2h.txt"),first.Where(x=>int.Parse(x.Split(':')[0])==1 && int.Parse(x.Split(':')[1].Split('|')[0])<660),new UTF8Encoding(false)); }
            File.WriteAllText(Path.Combine(folder,$"seed-{seed}.sha256"),Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",first)))));
        }
    }
    private sealed class ReplayResult
    {
        public InvestorSociety Society;
        public List<SocialMessage> Messages=new();
        public int FactErrors;
        public int AvailabilityErrors, ConcurrencyErrors;
        public SocialJournal Journal;
        public string Serialized => string.Join("\n",Messages.Select(JsonUtility.ToJson));
    }
    private static ReplayResult SocialReplay(int seed,int stage,bool priorGain=false,int steps=3324,Action<ReplayResult,int> afterStep=null)
    {
        ChatSocialIds.Reset();
        var journal=new SocialJournal(100000);ChatSocialAudit.Begin(journal);
        StockManager.Instance=new StockManager();
        for(int i=0;i<4;i++) { StockManager.Instance.stocks.Add(new Stock {symbol="S"+i,stockName=new[]{"노보손해보험","두덕리식품","미래바이오","국민금융"}[i],currentPrice=1000+i*500}); }
        var bots=new List<TradeBot>();
        for(int id=1;id<=20;id++)
        {
            var bot=new TradeBot {Social=new BotSocialState(id,seed,"RandomBot")};
            var stock=StockManager.Instance.stocks[id%4];
            bot.Positions[stock]=(100m,stock.currentPrice*(id%2==0?.9m:1.1m));
            bot.Social.Positions[stock]=new BotSocialState.PositionMemory();
            bot.Social.Psychology.Room=1;bots.Add(bot);
            if(priorGain) { bot.Social.Psychology.Record(new InvestorExperience(10000+id,10000+id,1,stock.symbol,100000m,120000m,0,"prior filled command fixture"),new InvestorSocietySettings()); }
        }
        var society=new InvestorSociety();society.BindSocialBots(bots);
        var result=new ReplayResult {Society=society,Journal=journal};
        society.SocialMessagePublished+=message=>
        {
            result.Messages.Add(message);
            var bot=society.SocialBots[message.senderId];
            if(!society.Present(bot,message.roomId) || message.targetId>0 && !society.Present(society.SocialBots[message.targetId],message.roomId)) { result.AvailabilityErrors++; }
            var active=society.SocialRoom(message.roomId).threads.Where(t=>t.Active).ToArray();
            if(active.Length>2 || active.Count(t=>t.kind==SocialThreadKind.ARGUMENT)>1 || active.SelectMany(t=>t.participants).GroupBy(id=>id).Any(g=>g.Count()>1)) { result.ConcurrencyErrors++; }
            var stock=StockManager.Instance.stocks.FirstOrDefault(s=>s.symbol==message.facts.symbol);
            if(stock!=null)
            {
                var current=society.CaptureFacts(bot,stock);
                if(current.price!=message.facts.price || current.quantity!=message.facts.quantity || current.amount!=message.facts.amount || current.pct!=message.facts.pct || current.held!=message.facts.held || current.bought!=message.facts.bought || current.sold!=message.facts.sold) { result.FactErrors++; }
            }
            if(message.text.Contains('{') || message.text.Contains('}') || message.debug==null || message.debug.weights==null || string.IsNullOrWhiteSpace(message.templateKey)) { result.FactErrors++; }
        };
        // The production scheduler receives fixed game/real timestamps. Portfolio inputs are deterministic fixtures,
        // not a substitute for the real-market, three-day test required in stage 7.
        for(int step=0;step<steps;step++)
        {
            double minute=540+step*390d/900d;UnityEngine.Time.time=step;
            society.SocialAdvance((int)(minute/1440)+1,minute%1440,step,20);
            afterStep?.Invoke(result,step);
        }
        return result;
    }
    private static void IgnoredFightScenario(int stage)
    {
        var replay=SocialReplay(73129,stage,true);
        var calls=replay.Messages.Where(m=>m.act==SocialAct.CALL_PLAYER).ToArray();
        var exits=replay.Journal.Events.Where(e=>e.kind=="VOLUNTARY_EXIT").ToArray();
        var changes=replay.Journal.Changes.Where(c=>c.causeType=="unmoderated-fight" && c.field=="trust" && c.after<c.before).ToArray();
        Check(exits.Any(e=>calls.Any(c=>c.roomId==e.room && c.at<e.time && c.debug.causeId==e.threadId) && changes.Any(c=>c.causeId==e.threadId)),
            $"S2 prior profitable fills + no player actions: calls={calls.Length}, exits={exits.Length}, witnessed trust losses={changes.Length}");
        string folder=Path.Combine(root,"docs/chat-social-results/stage-"+stage);
        File.WriteAllLines(Path.Combine(folder,"S2-ignored-fight.jsonl"),replay.Messages.Select(JsonUtility.ToJson).Concat(replay.Journal.Events.Select(JsonUtility.ToJson)).Concat(changes.Select(JsonUtility.ToJson)));
    }
    private static void SocialReplays(int stage)
    {
        string directory=Path.Combine(root,"docs/chat-social-results/stage-"+stage);Directory.CreateDirectory(directory);
        int index=0;
        int totalVoluntary=0,totalSympathy=0;
        foreach(int seed in new[]{73129,73130,73131,73132,73133})
        {
            var replay=SocialReplay(seed,stage);var duplicate=SocialReplay(seed,stage);
            Check(replay.Messages.Count>0 && replay.Serialized==duplicate.Serialized,$"T1 social production seed={seed}, messages={replay.Messages.Count}");
            bool unique=true;var rooms=new Dictionary<int,Queue<string>>();
            foreach(var message in replay.Messages)
            { if(!rooms.TryGetValue(message.roomId,out var recent)) { recent=new Queue<string>();rooms[message.roomId]=recent; } if(recent.Contains(message.templateKey)) { unique=false; } recent.Enqueue(message.templateKey);if(recent.Count>30) { recent.Dequeue(); } }
            Check(unique,$"T7 rolling 30 template keys seed={seed}");
            Check(replay.FactErrors==0,$"T8 live portfolio facts + complete metadata seed={seed}");
            double blocked=replay.Society.SocialAttempts==0?0:(double)replay.Society.TemplateBlocked/replay.Society.SocialAttempts;
            Check(blocked<=ChatSocialConfig.Current.Get("R8Max"),$"R8 seed={seed} blocked={replay.Society.TemplateBlocked}/{replay.Society.SocialAttempts} ({blocked:P3})");
            if(stage>=3)
            {
                var s=replay.Society;
                Check(replay.Messages.All(m=>m.targetId==0 || m.text.StartsWith("@"+(m.targetId==-1?"방장":s.SocialBots[m.targetId].Social.Nickname)+" ")),$"T2 target prefix seed={seed}");
                Check(replay.AvailabilityErrors==0,$"T10 room availability at publication seed={seed}");
                Check(replay.ConcurrencyErrors==0 && s.ClosedThreads.All(t=>t.turnCount<=t.maxTurns && t.participants.Count<=4),$"T11 thread turns + participants + concurrency seed={seed}");
                var roomMessages=replay.Messages.Where(m=>m.roomId==1).ToArray();
                var directed=roomMessages.Where(m=>m.targetId>0).ToArray();
                int answered=directed.Count(m=>roomMessages.Any(reply=>reply.replyToId==m.id && reply.senderId==m.targetId));
                var closed=s.ClosedThreads.Where(t=>t.roomId==1).ToArray();
                double r1=directed.Length==0?0:(double)answered/directed.Length;
                double r2=closed.Length==0?0:closed.Average(t=>t.turnCount);
                double r3=(double)roomMessages.Count(m=>m.targetId!=0)/roomMessages.Length;
                Check(r1>=ChatSocialConfig.Current.Get("R1Min") && r1<=ChatSocialConfig.Current.Get("R1Max"),$"R1 room=1 seed={seed} {answered}/{directed.Length} = {r1:P3}");
                Check(r2>=ChatSocialConfig.Current.Get("R2Min") && r2<=ChatSocialConfig.Current.Get("R2Max"),$"R2 room=1 seed={seed} mean={r2:F4}, threads={closed.Length}");
                Check(r3>=ChatSocialConfig.Current.Get("R3Min") && r3<=ChatSocialConfig.Current.Get("R3Max"),$"R3 seed={seed} targeted={r3:P3}");
                Check(s.ClosedThreads.Any(t=>t.turnCount>=3),$"scene A spontaneous multi-turn conversation seed={seed}");
            }
            if(stage>=4)
            {
                var changes=replay.Journal.Changes.ToArray();var events=replay.Journal.Events.ToArray();
                File.WriteAllLines(Path.Combine(directory,$"seed-{seed}-events.jsonl"),events.Select(JsonUtility.ToJson));
                bool stages=true;
                foreach(var departure in events.Where(e=>e.kind=="VOLUNTARY_EXIT"))
                {
                    var history=events.TakeWhile(e=>!ReferenceEquals(e,departure)).Where(e=>e.kind=="ANGER_STAGE" && e.actor==departure.actor && e.target==departure.target).Select(e=>(int)e.value).ToArray();
                    int expected=1;foreach(int value in history) { if(value==expected) { expected++; } }
                    stages &= expected==5;
                }
                Check(stages,$"T3 every voluntary departure has 1-2-3-4 cause chain seed={seed}");
                Check(changes.Where(c=>c.field=="anger" && c.causeType=="direct-message").All(c=>InvestorPsychology.Stage(c.after)-InvestorPsychology.Stage(c.before)<=1),$"T4 anger rises <=one stage per message seed={seed}");
                Check(events.Where(e=>e.kind=="SYMPATHY_EXIT" || e.kind=="SYMPATHY_SCHEDULED").All(e=>e.value>=60),$"T5 sympathy affinity seed={seed}");
                Check(events.Where(e=>e.kind=="SYMPATHY_EXIT").All(e=>events.Any(origin=>origin.kind=="VOLUNTARY_EXIT" && origin.actor==e.target && origin.time<=e.time)),$"T6 sympathy depth=1 seed={seed}");
                Check(changes.Where(c=>c.causeType=="witness-message" || c.causeType=="legacy-witness").All(c=>c.field!="betrayal" || c.after<=c.before),$"T9 no rumor betrayal seed={seed}");
                double r4=events.Count(e=>e.kind=="ARGUMENT" && e.room==1),r5=replay.Society.VoluntaryExits,r6=r5>0?replay.Society.SympathyExits/r5:0;
                Console.WriteLine($"Public-room arguments (outside the specified private-room R4): {events.Count(e=>e.kind=="ARGUMENT" && e.room==0)}");
                Check(r4>=ChatSocialConfig.Current.Get("R4Min") && r4<=ChatSocialConfig.Current.Get("R4Max"),$"R4 seed={seed} arguments={r4}");
                Check(r5>=ChatSocialConfig.Current.Get("R5Min") && r5<=ChatSocialConfig.Current.Get("R5Max"),$"R5 seed={seed} voluntary={r5}");
                totalVoluntary+=replay.Society.VoluntaryExits;totalSympathy+=replay.Society.SympathyExits;
                Console.WriteLine($"R6 sample seed={seed} sympathy={replay.Society.SympathyExits}, voluntary={r5}, ratio={r6:F3}");
            }
            if(stage>=6)
            {
                var roomMessages=replay.Messages.Where(m=>m.roomId==1).ToArray();
                double memoryRatio=(double)roomMessages.Count(m=>m.memory)/roomMessages.Length;
                Console.WriteLine($"Memory template candidates={replay.Society.MemoryEligibleAttempts}, blocked={replay.Society.MemoryTemplateBlocked}");
                Check(memoryRatio>=ChatSocialConfig.Current.Get("R7Min") && memoryRatio<=ChatSocialConfig.Current.Get("R7Max"),$"R7 room=1 seed={seed} memory={roomMessages.Count(m=>m.memory)}/{roomMessages.Length} = {memoryRatio:P3}");
            }
            if(index++<3) { File.WriteAllLines(Path.Combine(directory,$"seed-{seed}-2h.txt"),replay.Messages.Where(m=>m.at<660).Select(m=>$"{m.at:F1}|room={m.roomId}|#{m.senderId}|{m.act}|{m.text}"),new UTF8Encoding(false)); }
            File.WriteAllText(Path.Combine(directory,$"seed-{seed}.sha256"),Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(replay.Serialized))));
        }
        if(stage>=4)
        {
            double average=totalVoluntary>0?(double)totalSympathy/totalVoluntary:0;
            Check(average>=ChatSocialConfig.Current.Get("R6Min") && average<=ChatSocialConfig.Current.Get("R6Max"),$"R6 per voluntary-exit mean over all 5 seeds: {totalSympathy}/{totalVoluntary} = {average:F4}");
        }
    }
}
