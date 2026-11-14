using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class ChatSocialTests
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
            ModelTests(); LegacyReplay();
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
        { Check(relation.to!=1 && relation.affinity>=30 && relation.affinity<=70 && relation.anger==0,"friend seed bounds"); }
        for(int i=0;i<10;i++) { a.Remember(new SocialMemory { eventId=i+1,at=i,salience=i,type=SocialMemoryKind.FOUGHT }); }
        Check(a.Memories.Count==8 && a.Memories.Min(m=>m.salience)==2,"memory eviction lowest salience then oldest");
        var old=JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>("{\"version\":1,\"id\":1,\"room\":1,\"greed\":70,\"fear\":40,\"trust\":180,\"betrayal\":130,\"fomo\":80}");
        Check(a.RestoreSocial(old) && a.Trust==180 && a.Betrayal==130 && a.Fomo==0 && a.FomoUntil==0,"v1 migration keeps overheat values and discards numeric FOMO");
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
}
