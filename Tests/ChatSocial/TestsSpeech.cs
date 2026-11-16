using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using UnityEngine;

public static partial class ChatSocialTests
{
    private static bool CommunityLine(SocialMessage message) => message.debug?.speechKey?.StartsWith("community.", StringComparison.Ordinal) == true;
    private static SocialFacts SpeechFacts(bool gain = true) => new()
    {
        symbol = "NM", stockName = "노보손해보험", amount = gain ? "5,284,003" : "-5,284,003", pct = gain ? "+50.0" : "-50.0",
        price = "5,600", quantity = "1,234", avgPrice = "3,733", held = true, profitable = gain, losing = !gain, profitKind = "평가"
    };
    private static SocialMessage VoiceMessage(InvestorPsychology mind, long id, SocialAct act = SocialAct.BRAG, SocialFacts facts = null) => new()
    { id = id, senderId = mind.SocialId, act = act, facts = facts ?? SpeechFacts(), debug = new SocialMessageDebug() };
    private static SocialTemplate VoiceTemplate(string act = "BRAG") => ChatSocialAssets.Templates.templates.Single(t => t.key == act + ".casual.1.plain.02");
    private static void SpeechScenarios()
    {
        ChatSocialAssets.Reload();
        var population = Enumerable.Range(1, 1000).Select(id => new InvestorPsychology(id, 73129)).ToArray();
        var playful = population.First(m => !m.Traits.Serious); var serious = population.First(m => m.Traits.Serious);
        int seriousCount = population.Count(m => m.Traits.Serious);
        Check(seriousCount >= 400 && seriousCount <= 500, $"Stable speech trait population: serious={seriousCount}/1000, both types present");
        Check(population.All(m => m.Traits.Serious == new InvestorPsychology(m.SocialId, 73129).Traits.Serious), "Speech disposition repeats for the same seed and ID");
        Check(population.Any(m => m.Traits.Serious && m.Traits.Politeness < 34) && population.Any(m => !m.Traits.Serious && m.Traits.Politeness >= 67), "Seriousness is independent of polite/rough register");
        var oldRandom = new SocialRandom(98271); var newRandom = new SocialRandom(98271);
        foreach (string trait in new[] { "temper", "grudge", "suggestibility", "sociability", "bravado", "politeness" })
        { oldRandom.Normal(ChatSocialConfig.Current.Get(trait + "Mean"), ChatSocialConfig.Current.Get(trait + "Sd")); }
        _ = new SocialTraits(newRandom, ChatSocialConfig.Current);
        Check(oldRandom.State == newRandom.State, "Adding a voice trait consumes no decision/friendship RNG draws");
        var restored = new InvestorPsychology(serious.SocialId, 73129);
        var snapshot = JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>(JsonUtility.ToJson(serious.CaptureSocial()));
        Check(restored.RestoreSocial(snapshot) && restored.Traits.Serious && restored.SocialRng.State == serious.SocialRng.State, "New speech trait survives save/restore with social RNG intact");
        var oldSave = JsonNode.Parse(JsonUtility.ToJson(playful.CaptureSocial()));
        oldSave["traits"].AsObject().Remove("speechVersion"); oldSave["traits"].AsObject().Remove("serious");
        var migrated = new InvestorPsychology(playful.SocialId, 73129);
        Check(migrated.RestoreSocial(JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>(oldSave.ToJsonString())) && !migrated.Traits.Serious && migrated.Traits.Politeness == playful.Traits.Politeness, "Old v2 traits migrate without resetting existing personality");
        oldSave["version"] = 1;
        Check(migrated.RestoreSocial(JsonUtility.FromJson<InvestorPsychology.SocialSnapshot>(oldSave.ToJsonString())), "Old v1 social snapshot with traits remains loadable");
        snapshot.traits = JsonUtility.FromJson<SocialTraits>("{\"speechVersion\":99}");
        Check(!restored.RestoreSocial(snapshot), "Unknown speech save version is rejected without overwriting the bot");

        var template = VoiceTemplate(); int playfulLines = 0, seriousLines = 0; bool repeated = true, truthful = true;
        uint playfulState = playful.SocialRng.State, seriousState = serious.SocialRng.State;
        for (int id = 1; id <= 10000; id++)
        {
            var a = VoiceMessage(playful, id); var b = VoiceMessage(serious, id);
            string factsBefore = JsonUtility.ToJson(a.facts);
            string first = InvestorSociety.RenderSocialText(playful, template, a, null, "");
            string second = InvestorSociety.RenderSocialText(serious, template, b, null, "");
            if (CommunityLine(a)) { playfulLines++; }
            if (CommunityLine(b)) { seriousLines++; }
            repeated &= first == InvestorSociety.RenderSocialText(playful, template, a, null, "");
            truthful &= factsBefore == JsonUtility.ToJson(a.facts) && first.Contains("노보손해보험") && !first.Contains("{") && !second.Contains("{");
        }
        Check(playfulLines >= 6700 && playfulLines <= 7300, $"Playful eligible community rate: {playfulLines}/10000 (target 70%)");
        Check(seriousLines >= 140 && seriousLines <= 260, $"Serious eligible community rate: {seriousLines}/10000 (target 2%)");
        Check(repeated && playfulState == playful.SocialRng.State && seriousState == serious.SocialRng.State, "Voice/number choices are replayable and never advance social decision RNG");
        Check(truthful, "Colloquial messages preserve raw P/L metadata and resolve every slot");
        Check(ChatSocialNumbers.Amount("5,284,003", 0) == "500만 정도" && ChatSocialNumbers.Amount("5,284,003", 1) == "500장" && ChatSocialNumbers.Amount("5,284,003", 2) == "500 정도", "5,284,003 won uses natural approximate Korean amounts");
        Check(ChatSocialNumbers.Amount("50,000", 1) == "5장" && ChatSocialNumbers.Amount("5,284,003", 1) != "5장", "Jang consistently means ten thousand won, never a hundredfold error");
        Check(ChatSocialNumbers.Amount("-5,284,003", 0) == "500만 정도" && ChatSocialNumbers.Amount("-5,284,003", 0, signed: true).StartsWith("−500"), "Loss magnitude and signed P/L remain distinguishable");
        Check(ChatSocialNumbers.Return("+50.0", 0) == "50퍼" && ChatSocialNumbers.Return("+50.0", 1) == "50%" && ChatSocialNumbers.Return("-50.0", 1) == "−50%", "Spoken return drops redundant decimal/plus while retaining loss direction");
        Check(ChatSocialNumbers.Amount("0", 1) == "0원" && ChatSocialNumbers.Amount("0.5", 1) == "1원 미만" && ChatSocialNumbers.Return("-0.001", 0) == "−0.01퍼 미만", "Tiny nonzero results do not turn into a false zero");
        Check(ChatSocialNumbers.Amount("528", 0) == "500원 정도" && ChatSocialNumbers.Amount("5284", 0) == "5천 원 정도" && ChatSocialNumbers.Amount("528400300", 0) == "5억 정도", "Small and large amounts keep their Korean scale");
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Check(ChatSocialNumbers.Amount("5,284,003", 1) == "500장" && ChatSocialNumbers.Return("+50.0", 0) == "50퍼", "Chat amount parsing is independent of OS number culture");
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
        SpeechGuards(playful, serious); MentionScenarios(); VoiceReplayAndSamples();
        ChatSocialAssets.Reload();
    }
    private static void SpeechGuards(InvestorPsychology playful, InvestorPsychology serious)
    {
        var original = ChatSocialConfig.Current; var acts = ChatSocialAssets.Acts; var catalog = ChatSocialAssets.Templates;
        var config = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(original));
        config.values.Single(v => v.key == "communityPlayfulChance").value = 1;
        config.values.Single(v => v.key == "communitySeriousChance").value = 0;
        config.values.Single(v => v.key == "colloquialNumberChance").value = 1;
        ChatSocialAssets.Install(config, acts, catalog);
        try
        {
            var template = VoiceTemplate(); var message = VoiceMessage(playful, 51001);
            string text = InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(CommunityLine(message), "Probability one selects an eligible community phrase");
            var firstKey = message.debug.speechKey; playful.RecentSpeechKeys.Enqueue(firstKey);
            InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(message.debug.speechKey != firstKey, "Recent community wording is skipped without changing the semantic template");
            playful.RecentSpeechKeys.Clear();
            message = VoiceMessage(serious, 51002); InvestorSociety.RenderSocialText(serious, template, message, null, "");
            Check(!CommunityLine(message), "Probability zero never chooses community wording");
            message = VoiceMessage(playful, 51003, facts: SpeechFacts(false));
            InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(!CommunityLine(message), "Loss facts cannot select a gain boast from the community catalog");
            message = VoiceMessage(playful, 51004, SocialAct.LAMENT, SpeechFacts(false));
            text = InvestorSociety.RenderSocialText(playful, VoiceTemplate("LAMENT"), message, null, "");
            Check(CommunityLine(message) && !text.Contains("-5,284,003") && (text.Contains("손실") || text.Contains("마이너스") || text.Contains("−50")), "Loss complaint is colloquial without claiming a profit");
            message = VoiceMessage(playful, 51005); message.facts.signal = true;
            template = new SocialTemplate { key = "BRAG.casual.1.plain.02", act = "BRAG", style = "casual", variant = "plain", text = "{price}원 {quantity}주, {amount}원, {pct}%" };
            text = InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(!CommunityLine(message) && text.Contains("5,600원 1,234주, 5,284,003원, +50.0%"), "Signal/order facts retain exact prices, quantities, amounts and returns");
            template.promisedAction = SocialPromisedAction.Buy; message.facts.signal = false;
            text = InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(!CommunityLine(message) && text.Contains("5,600원 1,234주"), "Voice rewriting cannot erase an actual future-trade promise");
            template = VoiceTemplate(); message.memory = true;
            InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(!CommunityLine(message), "Memory narration is not replaced by an unrelated current result");
            message = VoiceMessage(playful, 51006); message.facts.notice = "PLAYER_KICK";
            InvestorSociety.RenderSocialText(playful, template, message, null, "");
            Check(!CommunityLine(message), "Moderation notice cannot turn into a boast or a joke about prices");
            message = VoiceMessage(playful, 51007, SocialAct.AGREE); message.targetId = 2;
            text = InvestorSociety.RenderSocialText(playful, VoiceTemplate("AGREE"), message, new SocialMessage { act = SocialAct.LAMENT }, "익명002");
            Check(message.debug.speechKey.StartsWith("community.LAMENT.AGREE") && !text.Contains("ㅋㅋ"), "Agreement with a loss uses an empathetic response rather than profit cheering");
            var watch = catalog.templates.Single(t => t.key == "watch.RISE.rough.1.01");
            var facts = SpeechFacts(); facts.notice = "WATCH_RISE"; facts.observationBotId = serious.SocialId; facts.observationPrice = "3,000"; facts.observationPercent = "+50.0"; facts.observedAt = 5;
            message = VoiceMessage(serious, 51008, SocialAct.REACT, facts);
            text = InvestorSociety.RenderSocialText(serious, watch, message, null, "");
            Check(!CommunityLine(message) && !text.Contains("ㅋㅋ") && !text.Contains("ㄷㄷ") && !text.Contains("뭐임"), "Serious watcher avoids pre-existing meme-style surprise outside its rare roll");
            message = VoiceMessage(playful, 51009, SocialAct.REACT, facts);
            InvestorSociety.RenderSocialText(playful, watch, message, null, "");
            Check(!CommunityLine(message), "Community rewrite cannot impersonate another bot's watch observation");
            facts.observationBotId = playful.SocialId;
            text = InvestorSociety.RenderSocialText(playful, watch, message, null, "");
            Check(CommunityLine(message) && text.Contains("노보손해보험") && !text.Contains("수익"), "Watch surprise uses observed movement, not invented portfolio profit");
        }
        finally { ChatSocialAssets.Install(original, acts, catalog); }
        var invalid = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(original)); invalid.values.Single(v => v.key == "communitySeriousChance").value = 1.1;
        bool rejected = false;
        try { ChatSocialAssets.Install(invalid, acts, catalog); } catch (ArgumentException) { rejected = true; }
        Check(rejected && ReferenceEquals(original, ChatSocialConfig.Current), "Out-of-range voice probability cannot replace the live config");
    }
    private static void MentionScenarios()
    {
        var mentions = new ChatSocialMentions(new[] { "개미", "개미왕", "노보호가256", "방장", "A<B" });
        string open = ChatSocialMentions.MarkOpen;
        Check(mentions.Format("@노보호가256 그 말 맞음") == open + "@노보호가256</mark> 그 말 맞음", "At-mention receives a translucent inline background");
        Check(mentions.Format("개미왕님, 방장은 어떻게 봄?") == open + "개미왕</mark>님, " + open + "방장</mark>은 어떻게 봄?", "Multiple mentions and Korean suffixes highlight only the nickname");
        Check(mentions.Format("개미왕님은 어떰? 개미왕") == open + "개미왕</mark>님은 어떰? " + open + "개미왕</mark>", "Longest nickname match wins and repeated mentions are retained");
        Check(mentions.Format("개미왕국 노보호가2560 익명999") == "개미왕국 노보호가2560 익명999", "Partial names, longer IDs and unknown names remain plain text");
        Check(mentions.Format("hello 개미왕 ㅋㅋ\n@방장") == "hello " + open + "개미왕</mark> ㅋㅋ\n" + open + "@방장</mark>", "Nickname highlight works across wrapped/multiline messages");
        string hostile = mentions.Format("<size=999>개미왕</size> </noparse><mark=#FFFFFFFF> @방장");
        Check(!hostile.Contains("<size=999>") && !hostile.Contains("<mark=#FFFFFFFF>") && hostile.Contains("<noparse><</noparse>/noparse>"), "Message text cannot inject TMP size, color or noparse tags");
        Check(mentions.Format("A<B") == open + "A<noparse><</noparse>B</mark>" && mentions.Format(null) == "", "Special-character nickname and empty message are safe");
        var many = new ChatSocialMentions(Enumerable.Range(1, 1200).Select(i => "익명" + i.ToString("D4")));
        string sample = "@익명1191 익명0001님 ㅋㅋ 익명11910은 누구";
        Check(many.Format(sample).Split("<mark=").Length - 1 == 2, "1200-bot roster highlights exact names without scanning all bots per character");
    }
    private static void VoiceReplayAndSamples()
    {
        var on = SocialReplay(73129, 8, steps: 1800);
        var config = JsonUtility.FromJson<ChatSocialConfig>(JsonUtility.ToJson(ChatSocialConfig.Current));
        foreach (var row in config.values.Where(v => v.key == "communityPlayfulChance" || v.key == "communitySeriousChance" || v.key == "colloquialNumberChance" || v.key == "seriousColloquialNumberChance")) { row.value = 0; }
        ChatSocialAssets.Install(config, ChatSocialAssets.Acts, ChatSocialAssets.Templates);
        var off = SocialReplay(73129, 8, steps: 1800);
        Check(on.Messages.Select(m => $"{m.id}:{m.senderId}:{m.act}:{m.targetId}:{m.threadId}:{m.templateKey}").SequenceEqual(off.Messages.Select(m => $"{m.id}:{m.senderId}:{m.act}:{m.targetId}:{m.threadId}:{m.templateKey}")), "Voice enabled/disabled leaves all message decisions, replies and threads unchanged");
        Check(on.Journal.Changes.Select(JsonUtility.ToJson).SequenceEqual(off.Journal.Changes.Select(JsonUtility.ToJson)) && on.FactErrors == 0 && off.FactErrors == 0, "Cosmetic dialect does not change emotions, economic facts or event effects");
        Check(on.Messages.Any(CommunityLine) && !off.Messages.Any(CommunityLine), "Production scheduler publishes community voices only when enabled");
        string folder = Path.Combine(root, "Validation/ChatVoice"); Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, "conversation-samples.txt"), on.Messages.Where(CommunityLine).Take(45).Select(m => $"[{m.debug.speechDisposition}] #{m.senderId} {m.text} ({m.debug.speechKey})"));
        File.WriteAllLines(Path.Combine(folder, "conversation-raw.jsonl"), on.Messages.Select(JsonUtility.ToJson));
    }
}
