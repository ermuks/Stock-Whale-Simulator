using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static T Private<T>(object source, string name) => (T)source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
    private static void PolishScenarios()
    {
        ChatSocialAssets.Reload();
        var catalog = ChatSocialAssets.Templates; var config = ChatSocialConfig.Current; var acts = ChatSocialAssets.Acts;
        var counts = new List<string>(); bool plain = true, memory = true, unique = true, indexed = true;
        foreach (SocialAct act in Enum.GetValues(typeof(SocialAct)))
        {
            foreach (string style in new[] { "formal", "casual", "rough" })
            {
                for (int intensity = 1; intensity <= 3; intensity++)
                {
                    var rows = catalog.templates.Where(t => t.act == act.ToString() && t.style == style && t.intensity == intensity && t.variant == "plain").ToArray();
                    var memories = catalog.templates.Where(t => t.act == act.ToString() && t.style == style && t.intensity == intensity && t.variant == "memory").ToArray();
                    plain &= rows.Length >= 6; unique &= rows.Select(t => t.text).Distinct().Count() >= 6;
                    if (new[] { SocialAct.MOCK, SocialAct.REBUT, SocialAct.DOUBT, SocialAct.WARN_PLAYER }.Contains(act)) { memory &= memories.Length >= 4; }
                    indexed &= rows.SequenceEqual(ChatSocialAssets.Candidates(act, style, intensity, "plain")) && memories.SequenceEqual(ChatSocialAssets.Candidates(act, style, intensity, "memory"));
                    counts.Add($"{act},{style},{intensity},{rows.Length},{memories.Length},{rows.Select(t => t.text).Distinct().Count()}");
                }
            }
        }
        Check(plain && unique, "4.4 all 162 act/register/intensity groups have >=6 distinct plain texts");
        Check(memory, "4.4 all 36 targeted attack/doubt groups have >=4 memory texts");
        Check(indexed, "Template index preserves original eligibility scan order in every bucket");
        var agreement = catalog.templates.Single(t => t.key == "AGREE.casual.1.plain.01");
        var messageContext = new SocialMessage { act = SocialAct.AGREE, targetId = 2, debug = new SocialMessageDebug() };
        string smallTalk = InvestorSociety.ContextualSocialText(agreement, messageContext, new SocialMessage { act = SocialAct.SMALLTALK });
        string loss = InvestorSociety.ContextualSocialText(agreement, messageContext, new SocialMessage { act = SocialAct.LAMENT });
        Check(smallTalk != agreement.text && loss != smallTalk && messageContext.debug.contextKey == "LAMENT>AGREE.casual.1", "Reply wording distinguishes everyday talk from a loss without changing act or template key");
        var standalone = catalog.templates.Single(t => t.key == "DOUBT.rough.1.plain.01");
        messageContext = new SocialMessage { act = SocialAct.DOUBT, facts = new SocialFacts { symbol="S1" }, debug = new SocialMessageDebug() };
        Check(InvestorSociety.ContextualSocialText(standalone, messageContext, null).Contains("{symbol}"), "Untargeted doubt names its topic instead of addressing an absent claim");
        var exceptional = new SocialTemplate { key="AGREE.casual.1.plain.00", act="AGREE", style="casual", text="원문" };
        messageContext.act = SocialAct.AGREE;
        Check(InvestorSociety.ContextualSocialText(exceptional, messageContext, new SocialMessage { act=SocialAct.SMALLTALK }) == "원문", "Custom zero-index context key safely falls back");
        exceptional.key="AGREE.casual.1.plain.01"; exceptional.promisedAction=SocialPromisedAction.Buy;
        Check(InvestorSociety.ContextualSocialText(exceptional, messageContext, new SocialMessage { act=SocialAct.SMALLTALK }) == "원문", "Context decoration cannot erase a typed trade promise");
        var folder = Path.Combine(root, "docs/chat-social-results/stage-8"); Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder,"template-counts.csv"),new[]{"act,style,intensity,plain,memory,distinctPlain"}.Concat(counts),new UTF8Encoding(false));
        int generation = ChatSocialAssets.Generation;
        var badConfig = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(config));
        badConfig.values.Single(v => v.key == "hotWindowMinutes").value = 0;
        bool rejected = false;
        try { ChatSocialAssets.Install(badConfig, acts, catalog); } catch (ArgumentException) { rejected = true; }
        Check(rejected && ChatSocialAssets.Generation == generation && ReferenceEquals(config, ChatSocialConfig.Current), "Invalid window reload preserves running configuration and generation");
        var badCatalog = JsonUtility.FromJson<SocialTemplateDocument>(JsonUtility.ToJson(catalog)); badCatalog.templates[0].style = "typo";
        rejected = false;
        try { ChatSocialAssets.Install(config, acts, badCatalog); } catch (ArgumentException) { rejected = true; }
        Check(rejected && ChatSocialAssets.Generation == generation && ReferenceEquals(catalog, ChatSocialAssets.Templates), "Invalid template reload preserves catalog/index atomically");
        var mind = Enumerable.Range(1,100).Select(id => new InvestorPsychology(id,73129)).First(m => m.Traits.Politeness < 34 && !m.Traits.Serious);
        var save = mind.CaptureSocial(); save.greed = 90; save.fear = 90; mind.RestoreSocial(save);
        uint state = mind.SocialRng.State; int decorated = 0;
        for (int i = 1; i <= 1000; i++)
        {
            var message = new SocialMessage { id = i, senderId = mind.SocialId, act = SocialAct.BRAG };
            string first = InvestorSociety.DecorateSocialText(mind, message, "테스트");
            if (first != "테스트") { decorated++; }
            if (first != InvestorSociety.DecorateSocialText(mind, message, "테스트")) { throw new Exception("Cosmetic replay mismatch"); }
        }
        Check(decorated > 150 && decorated < 350 && state == mind.SocialRng.State, $"Suffixes deterministic, 1000-message coverage={decorated}, social RNG untouched");
        var neutral = new SocialMessage { id = 3, act = SocialAct.SMALLTALK };
        Check(InvestorSociety.DecorateSocialText(mind, neutral, "일상") == "일상", "Neutral small talk never receives unrelated fear/anger suffix");
        int notices = 0; var sink = new SocialLogSink(new BrokenWriter(), _ => notices++);
        sink.Write("first"); sink.Write("second"); sink.Flush(); sink.Dispose();
        Check(notices == 1 && !sink.Available, "Disk I/O failure disables only disk logging and warns once");
        var buffer = new StringWriter(); using (var healthy = new SocialLogSink(buffer, _ => notices++)) { healthy.Write("event"); healthy.Flush(); }
        Check(buffer.ToString().Contains("event") && notices == 1, "Healthy audit sink writes and disposes normally");
        PromiseScenarios(); CacheScenarios();
        ChatSocialAssets.Reload();
    }
    private sealed class BrokenWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string text) { throw new IOException("fixture: disk full"); }
        protected override void Dispose(bool disposing) { throw new IOException("fixture: flush failed"); }
    }
    private static void PromiseScenarios()
    {
        var replay = SocialReplay(73129, 8, steps:0); var bot = replay.Society.SocialBots[1]; var mind = bot.Social.Psychology;
        mind.PromiseTrade(SocialPromisedAction.Buy, "S1", 901);
        Check(mind.ConsumePromisedBuyProbability("S0", .3) == .3 && mind.PromisedAction == SocialPromisedAction.Buy, "Other stock does not consume a trade promise");
        Check(Math.Abs(mind.ConsumePromisedBuyProbability("S1", .3)-.6) < .00001 && mind.ConsumePromisedBuyProbability("S1", .3) == .3, "Buy promise doubles the next matching decision once");
        mind.PromiseTrade(SocialPromisedAction.Sell, "S1", 902);
        Check(Math.Abs(mind.ConsumePromisedBuyProbability("S1", .7)-.4) < .00001, "Sell promise doubles sell probability, not order size or money");
        mind.PromiseTrade(SocialPromisedAction.Buy, "S1", 903); var saved = mind.CaptureSocial(); mind.ClearPromise();
        Check(mind.RestoreSocial(saved) && mind.PromiseMessage == 903 && mind.PromisedSymbol == "S1", "Optional v2 promise survives social snapshot restore");
        Check(mind.ConsumePromisedBuyProbability("S1", .8) == 1, "Promise probability remains bounded at one");
        mind.PromiseTrade(SocialPromisedAction.Buy, "S1", 904); mind.ClearPromise("stock-unavailable");
        Check(mind.PromisedAction==SocialPromisedAction.None && replay.Journal.Events.Any(e=>e.kind=="TRADE_PROMISE_CANCELED" && e.id==904), "Unavailable trade promise cancels with an auditable reason");
        var original = ChatSocialAssets.Templates; var config = ChatSocialConfig.Current; var acts = ChatSocialAssets.Acts;
        var fixture = JsonUtility.FromJson<SocialTemplateDocument>(JsonUtility.ToJson(original));
        foreach (var row in fixture.templates.Where(t => t.act == "REACT" && t.variant == "plain"))
        { row.promisedAction = SocialPromisedAction.Buy; row.requires = Array.Empty<string>(); row.text = "다음 판단에서 {symbol} 매수를 시도하겠습니다."; }
        ChatSocialAssets.Install(config, acts, fixture);
        var facts = replay.Society.CaptureFacts(bot, StockManager.Instance.stocks[1]);
        replay.Society.Offer(bot, 1, SocialMotive.EMOTION_THRESHOLD, facts, SocialAct.REACT);
        replay.Society.SocialAdvance(1, 540, 0, 20);
        var published = replay.Messages.FirstOrDefault(m => m.senderId == 1 && m.promisedAction == SocialPromisedAction.Buy);
        Check(published != null && mind.PromiseMessage == published.id && mind.PromisedSymbol == published.facts.symbol, "Typed future-action template registers an actual pending decision before publication");
        ChatSocialAssets.Install(config, acts, original);
        string detail = ChatSocialDiagnostics.Message(published), investor = ChatSocialDiagnostics.Bot(bot);
        Check(detail.Contains("Trigger:") && detail.Contains("Facts at publication:") && detail.Contains("template:") && investor.Contains("Top 5 anger") && investor.Contains("Salient memories:"), "Developer click trace includes trigger, weights, facts, traits, top relations and memories");
    }
    private static void CacheScenarios()
    {
        var replay = SocialReplay(73129, 8, steps:0); var society = replay.Society;
        var threads = Private<Dictionary<long,SocialThread>>(society,"socialThreads");
        for (int i = 1; i <= 1100; i++)
        {
            var thread = new SocialThread { id=i, roomId=1, lastAt=0, endReason=SocialEnd.NO_REPLY };
            society.ClosedThreads.Add(thread); threads.Add(i, thread); society.SocialRoom(1).threads.Add(thread);
        }
        var active = new SocialThread { id=1200, roomId=1, lastAt=999 };
        threads.Add(1200,active); society.SocialRoom(1).threads.Add(active);
        society.SocialBots[1].Social.Psychology.ExitAt=1050; society.SocialBots[1].Social.Psychology.ExitThread=1;
        society.SocialRoom(1).recent.Enqueue(new SocialMessage { id=10, threadId=2 });
        var exposures = Private<Dictionary<(long,int,string),double>>(society,"threadExposure");
        exposures[(-10,1,"trust")]=5; exposures[(-9,1,"trust")]=5; exposures[(3,1,"trust")]=5;
        society.SocialTime.Set(1,1200,1000); society.TrimSocialCaches(true);
        Check(society.ClosedThreads.Count==512 && society.CachedThreadCount==513 && society.FindSocialThread(1200).Active, "1100 closed-thread soak bounded to 512 while active thread survives");
        Check(society.FindSocialThread(1)!=null && society.FindSocialThread(2)!=null && society.FindSocialThread(3)==null, "Pending departure and visible message protect their old thread");
        Check(exposures.Count==1 && exposures.ContainsKey((-10,1,"trust")), "Expired exposure is reclaimed; visible-message dedup cap survives");
        Check(society.SocialRoom(1).threads.Count==513, "Room thread cache is pruned consistently with global index");
    }
}
