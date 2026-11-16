using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static void ConversationFlowScenarios()
    {
        ChatSocialAssets.Reload();
        StockLinkScenarios(); TopicFlowScenarios(); TypingScenarios();
    }
    private static void StockLinkScenarios()
    {
        var formatter = new ChatSocialMentions(new[] { "개미왕", "NM" }, new[] {
            new KeyValuePair<string, string>("노보손해보험", "NM"), new KeyValuePair<string, string>("NM", "NM"),
            new KeyValuePair<string, string>("미래바이오", "MBI"), new KeyValuePair<string, string>("미래바이오홀딩스", "MBIH") });
        string text = formatter.Format("개미왕님 노보손해보험은 NM보다 미래바이오홀딩스 보고 있음");
        Check(text.Split("<link=").Length - 1 == 3 && text.Contains(ChatSocialMentions.MarkOpen + "개미왕</mark>") && text.Contains("stock:TUJJSA=="), "Names/tickers link alongside nickname marks; longest actual stock name wins");
        Check(formatter.Format("@NM NM") == ChatSocialMentions.MarkOpen + "@NM</mark> <link=\"stock:Tk0=\">" + ChatSocialMentions.StockMarkOpen + "NM</color></mark></link>", "Explicit at-mention stays a nickname even if it matches a ticker");
        Check(formatter.Format("NMZ 노보손해보험사") == "NMZ 노보손해보험사", "Partial or unknown stock names do not become clickable");
        string injected = formatter.Format("<link=\"stock:Tk0=\">fake</link> 노보손해보험");
        Check(injected.Split("<link=").Length - 1 == 1 && injected.Contains("<noparse><</noparse>link="), "Only roster-generated links are executable; chat markup is quoted");
        Check(ChatSocialMentions.TryStockSymbol("stock:Tk0=", out string symbol) && symbol == "NM", "Stock link restores the exact symbol");
        Check(!ChatSocialMentions.TryStockSymbol("stock:!bad!", out _) && !ChatSocialMentions.TryStockSymbol("stock:/w==", out _) &&
            !ChatSocialMentions.TryStockSymbol("http:Tk0=", out _) && !ChatSocialMentions.TryStockSymbol("stock:", out _), "Invalid link/base64/UTF8/empty symbol safely rejected");
    }
    private static void TopicFlowScenarios()
    {
        var replay = SocialReplay(73129, 8, steps: 0); var society = replay.Society;
        while (StockManager.Instance.stocks.Count < 8)
        { int id = StockManager.Instance.stocks.Count; StockManager.Instance.stocks.Add(new Stock { symbol = "S" + id, stockName = "종목" + id }); }
        double minute = 0;
        var market = new MarketConversationFocus { GameMinutes = () => minute };
        var stocks = StockManager.Instance.stocks;
        for (int t = 0; t < 60; t++)
        {
            minute = t;
            foreach (var stock in stocks) { market.Record(stock, 1000, 100, t); }
            market.Read(stocks[0], t);
        }
        society.ConfigureLiveConversation(market); society.SocialTime.Set(1, minute, 60);
        var focus = society.TopicFocus(0);
        for (int i = 0; i < 3; i++) { for (int j = 0; j < 8; j++) { focus.Observe(stocks[i].symbol, 60); } }
        Check(!focus.IntroducingAnother("S2", 60) && focus.IntroducingAnother("S3", 60), "Three familiar subjects encourage hesitation for a fourth without banning it");
        Check(focus.Weight("S3", 0, 60) > 0 && focus.Weight("S3", 1, 60) == focus.Weight("S0", 0, 60), "Every new topic keeps a chance; a hot newcomer can match a familiar topic");
        var bot = society.SocialBots[1]; int familiar = 0, outside = 0;
        for (int i = 0; i < 10000; i++)
        { var chosen = society.ChooseRoomTopic(bot, 0, (i + .5) / 10000); if (stocks.IndexOf(chosen) < 3) { familiar++; } else { outside++; } }
        Check(familiar >= 8500 && outside > 0, $"Soft focus weighted choices: familiar={familiar}, other={outside}/10000 (not a three-topic cap)");
        Check(!society.TopicFocus(1).IntroducingAnother("S3", 60) && society.TopicFocus(1).Weight("S3", 0, 60) == 1, "Each room maintains independent conversational attention");
        minute = 61;
        foreach (var stock in stocks) { market.Record(stock, stock == stocks[3] ? 1500 : 1000, stock == stocks[3] ? 3000 : 100, 63); }
        society.SocialTime.Set(1, minute, 63); int hot = 0;
        for (int i = 0; i < 10000; i++) { if (society.ChooseRoomTopic(bot, 0, (i + .5) / 10000) == stocks[3]) { hot++; } }
        Check(hot > outside * 2 && market.Read(stocks[3], 63).ReturnPercent == 50, $"Actual volume/price shock brings a new stock into attention: {hot}/10000 choices");
        var quietIntent = new SocialIntent { roomId = 0, due = 63, expires = 100, causeId = 1, facts = society.CaptureFacts(bot, stocks[4]) };
        bool delayed = false;
        for (int cause = 1; cause <= 30 && !delayed; cause++)
        {
            quietIntent.causeId = cause; quietIntent.topicHesitated = false;
            delayed = (bool)InvokeSociety(society, "HesitateOnNewTopic", bot, quietIntent);
        }
        Check(delayed && quietIntent.due >= 65 && quietIntent.due <= 70 && quietIntent.expires == 100 &&
            !(bool)InvokeSociety(society, "HesitateOnNewTopic", bot, quietIntent), "New-topic hesitation delays once, does not silently cancel or extend event lifetime");
        var shock = new SocialIntent { roomId = 0, facts = society.CaptureFacts(bot, stocks[4]) }; shock.facts.notice = "WATCH_RISE";
        Check(!(bool)InvokeSociety(society, "HesitateOnNewTopic", bot, shock), "Personal sudden-move observation can interrupt a quiet conversation");
        Check(!focus.IntroducingAnother("S7", 2000) && focus.Weight("S7", 0, 2000) == 1, "Old focus decays completely so yesterday's topics do not freeze the room");
        Console.WriteLine($"Focus measurement: familiar={familiar/100d:F2}%, outside={outside/100d:F2}%, new hot stock={hot/100d:F2}%");
    }
    private static void TypingScenarios()
    {
        var replay = SocialReplay(73129, 8, steps: 0); var society = replay.Society;
        var market = new MarketConversationFocus(); foreach (var stock in StockManager.Instance.stocks) { market.Register(stock); }
        society.ConfigureLiveConversation(market); society.SocialTime.Set(1, 600, 100);
        var a = society.SocialBots[1]; var b = society.SocialBots[2]; var stockA = StockManager.Instance.stocks[1];
        SocialIntent Intent(TradeBot bot, int room = 1) => new SocialIntent { roomId = room, due = society.SocialTime.Seconds,
            expires = society.SocialTime.Seconds + 60, motive = SocialMotive.SELF_EVENT, forcedAct = SocialAct.REACT, facts = society.CaptureFacts(bot, stockA) };
        InvokeSociety(society, "BeginTyping", a, Intent(a));
        Check(replay.Messages.Count == 0 && society.ReadTyping(1).Count == 1 && society.ReadTyping(1).Text == a.Social.Nickname + "님이 채팅 입력 중...", "Single typing indicator precedes any actual publication");
        for (int id = 2; id <= 4; id++) { InvokeSociety(society, "BeginTyping", society.SocialBots[id], Intent(society.SocialBots[id])); }
        Check(society.ReadTyping(1).Count == 4 && society.ReadTyping(1).Text == "4명이 입력 중..." && society.ReadTyping(0).Text == "", "Multiple actual waiting authors use the count; another room stays empty");
        InvokeSociety(society, "BeginTyping", society.SocialBots[5], Intent(society.SocialBots[5]));
        Check(society.ReadTyping(1).Count == 4 && society.SocialBots[5].Social.Psychology.Intentions.Count == 1, "Typing queue is bounded and overflow stays an intention");
        stockA.currentPrice += 321;
        society.SocialTime.Set(1, 601, 100.2); InvokeSociety(society, "FlushTyping");
        Check(replay.Messages.Count == 0, "Typing is a real delay rather than a label shown after sending");
        society.SocialTime.Set(1, 602, 103); InvokeSociety(society, "FlushTyping");
        Check(replay.Messages.Count == 2 && replay.Messages.All(m => m.facts.price == InvestorSociety.Money(stockA.currentPrice)) &&
            society.ReadTyping(1).Count == 2, "Publication refreshes live facts and honors existing two-per-ten-second quota");
        society.SocialTime.Set(1, 603, 114); InvokeSociety(society, "FlushTyping");
        Check(replay.Messages.Count == 4 && society.ReadTyping(1).Count == 0 && society.ReadTyping(1).Text == "", "Waiting authors send in a later quota window and the indicator clears");
        int published = replay.Messages.Count;
        InvokeSociety(society, "BeginTyping", a, Intent(a)); a.Social.Psychology.Room = 0;
        InvokeSociety(society, "FlushTyping");
        Check(society.ReadTyping(1).Count == 0 && replay.Messages.Count == published, "Leaving the room cancels in-progress typing without a ghost message");
        a.Social.Psychology.Room = 1; InvokeSociety(society, "BeginTyping", a, Intent(a));
        a.Social.Psychology.MutedUntil[1] = 700; InvokeSociety(society, "FlushTyping");
        Check(society.ReadTyping(1).Count == 0 && replay.Messages.Count == published, "Muted author disappears from the waiting indicator");
        a.Social.Psychology.MutedUntil.Clear(); var expiring = Intent(a); expiring.expires = 114.1;
        InvokeSociety(society, "BeginTyping", a, expiring); society.SocialTime.Set(1, 604, 115); InvokeSociety(society, "FlushTyping");
        Check(society.ReadTyping(1).Count == 0 && replay.Messages.Count == published, "Expired facts do not publish because they had begun typing");
        InvokeSociety(society, "BeginTyping", b, Intent(b)); stockA.IsDelisted = true; InvokeSociety(society, "FlushTyping");
        Check(society.ReadTyping(1).Count == 0 && replay.Messages.Count == published, "Delisting invalidates a queued stock conversation");
        var live = SocialReplay(901, 8, steps: 0); var s = live.Society; var leader = s.SocialBots[1];
        s.ConfigureLiveConversation(new MarketConversationFocus());
        s.Offer(leader, 1, SocialMotive.SELF_EVENT, s.CaptureFacts(leader, StockManager.Instance.stocks[0]), SocialAct.REACT, cause: 44);
        s.SocialAdvance(1, 540, 0, 20);
        Check(live.Messages.All(m => m.senderId != leader.Social.Id) && s.ReadTyping(1).Count > 0, "Real production scheduler routes selected utterances through typing");
        s.SocialAdvance(1, 541, 3, 20);
        Check(live.Messages.Any(m => m.senderId == leader.Social.Id), "Production scheduler publishes the prepared utterance on a later tick");
    }
    private static void VoiceVarietyScenarios()
    {
        ChatSocialAssets.Reload();
        var config = ChatSocialConfig.Current; var acts = ChatSocialAssets.Acts; var templates = ChatSocialAssets.Templates;
        Check(templates.voices.Length == 60 && templates.voices.Sum(v => v.texts.Length) == 240 && templates.voices.All(v => v.texts.Distinct().Count() == 4), "Six voices cover both formality levels and five contexts with 240 phrases");
        var minds = Enumerable.Range(1, 120).Select(id => new InvestorPsychology(id, 73129)).ToArray();
        Check(minds.Select(InvestorSociety.VoiceKey).Distinct().Count() == 6, "Bot population contains all six stable speaking styles");
        Check(InvestorSociety.VoiceStockParticles("{symbol}이 계속 신경 쓰임. 결국 {symbol}으로 돌아옴", "미래바이오") ==
            "{symbol}가 계속 신경 쓰임. 결국 {symbol}로 돌아옴", "Vowel-ending stock name uses correct subject/direction particles");
        Check(InvestorSociety.VoiceStockParticles("{symbol}은 {symbol}을 {symbol}이네요", "미래바이오") == "{symbol}는 {symbol}를 {symbol}네요" &&
            InvestorSociety.VoiceStockParticles("{symbol}은 {symbol}을 {symbol}으로", "노보손해보험") == "{symbol}은 {symbol}을 {symbol}으로" &&
            InvestorSociety.VoiceStockParticles("{symbol}으로", "서울") == "{symbol}로", "Topic/object/copula particles retain consonants and handle final rieul");
        bool stable = true, guard = true; var samples = new List<string>();
        var fixture = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(config));
        fixture.values.Single(v => v.key == "communityPlayfulChance").value = 0;
        fixture.values.Single(v => v.key == "communitySeriousChance").value = 0;
        ChatSocialAssets.Install(fixture, acts, templates);
        try
        {
            foreach (var mind in minds)
            {
                uint before = mind.SocialRng.State;
                string key = InvestorSociety.VoiceKey(mind);
                var copy = new InvestorPsychology(mind.SocialId, 999); copy.RestoreSocial(mind.CaptureSocial());
                stable &= key == InvestorSociety.VoiceKey(copy) && before == mind.SocialRng.State;
                var message = VoiceMessage(mind, 90000 + mind.SocialId, SocialAct.REACT); message.facts.notice = "FOMO";
                string text = InvestorSociety.RenderSocialText(mind, VoiceTemplate("REACT"), message, null, "");
                guard &= message.debug.speechKey.StartsWith("voice.") && message.debug.speechVoice == InvestorSociety.VoiceLabel(mind) && !text.Contains("{") && !text.Contains("ㅋㅋ") && !text.Contains("ㄷㄷ");
                if (!samples.Any(row => row.StartsWith(key + " |"))) { samples.Add(key + " | " + text); }
                string prior = message.debug.speechKey; mind.RecentSpeechKeys.Enqueue(prior);
                InvestorSociety.RenderSocialText(mind, VoiceTemplate("REACT"), message, null, "");
                guard &= prior != message.debug.speechKey;
            }
            Check(stable, "Voice remains stable through save/load and never consumes decision RNG");
            Check(guard, "FOMO expressions resolve facts, avoid recent wording and keep serious speech free of meme fillers");
        }
        finally { ChatSocialAssets.Install(config, acts, templates); }
        var on = SocialReplay(73129, 8, steps: 1800);
        var disabled = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(config)); disabled.values.Single(v => v.key == "voiceStyleChance").value = 0;
        ChatSocialAssets.Install(disabled, acts, templates);
        var off = SocialReplay(73129, 8, steps: 1800);
        Check(on.Messages.Select(m => $"{m.id}:{m.senderId}:{m.act}:{m.templateKey}:{m.targetId}:{m.threadId}").SequenceEqual(off.Messages.Select(m => $"{m.id}:{m.senderId}:{m.act}:{m.templateKey}:{m.targetId}:{m.threadId}")) &&
            on.Journal.Changes.Select(JsonUtility.ToJson).SequenceEqual(off.Journal.Changes.Select(JsonUtility.ToJson)), "Phrase variety changes wording while keeping decision/emotion replay identical");
        ChatSocialAssets.Install(config, acts, templates);
        var malformed = JsonUtility.FromJson<SocialTemplateDocument>(JsonUtility.ToJson(templates)); malformed.voices[0].voice = "unknown";
        bool rejected = false;
        try { ChatSocialAssets.Install(config, acts, malformed); } catch (ArgumentException) { rejected = true; }
        Check(rejected && ReferenceEquals(ChatSocialAssets.Templates, templates), "Invalid voice catalog cannot replace installed assets");
        string output = Path.Combine(root, "Validation/ChatVoiceVariety"); Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, "renderer-samples.txt"), samples, new UTF8Encoding(false));
    }
}
