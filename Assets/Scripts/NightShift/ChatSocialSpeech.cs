using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public sealed partial class InvestorSociety
{
    private static readonly Regex SpokenNumberPattern = new(@"\{(amount|memAmount|pct|memPct)\}(?:원|%)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static SocialRandom CosmeticRandom(InvestorPsychology mind, SocialMessage message, uint salt)
    {
        uint seed = unchecked((uint)message.id ^ (uint)(message.id >> 32) ^ (uint)mind.SocialId * 486187739u ^ salt);
        seed ^= seed >> 16; seed = unchecked(seed * 0x7feb352du); seed ^= seed >> 15; seed = unchecked(seed * 0x846ca68bu); seed ^= seed >> 16;
        return new SocialRandom(unchecked((int)seed));
    }
    public static string RenderSocialText(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, string target, IEnumerable<SocialMessage> recent = null)
    {
        message.debug ??= new SocialMessageDebug();
        message.debug.speechDisposition = mind.Traits.SpeechLabel; message.debug.speechKey = null; message.debug.speechVoice = VoiceLabel(mind);
        var random = CosmeticRandom(mind, message, 0x58c43af1u);
        // 인증 카드가 붙은 말에 다는 답은 수익률만이 아니라 금액도 보고 한다(ChatProofReaction.cs): 푼돈이면 가볍게 놀리고, 큰돈이면 놀란다.
        if (TryProofReply(mind, template, message, parent, out string proofLine)) { return proofLine; }
        // 새벽 관찰실의 말은 그 방의 대사로 한다(ChatDawnRoom.cs): 지금의 실제 값을 대고, 물음에는 어쩔 것인지를 답한다.
        if (TryDawnLine(mind, template, message, parent, recent, out string dawnLine)) { return dawnLine; }
        // 라운지에서 남의 말에 다는 맞장구는 가끔 한두 마디로 끝난다("ㄹㅇ", "그니까", "fr", "それな")(ChatProofReaction.cs의 TryBackchannel). 몇 초에 한 번만.
        if (TryBackchannel(mind, template, message, parent, out string backLine)) { return backLine; }
        double communityChance = ChatSocialConfig.Current.Get(mind.Traits.Serious ? "communitySeriousChance" : "communityPlayfulChance");
        string phrase = ContextualSocialText(template, message, parent);
        bool community = false;
        // 시세를 말할 수 있으면 성격과 상관없이 대부분 시세를 말한다. 난수는 먼저 뽑아 두어 다른 선택의 순서가 바뀌지 않게 한다.
        bool communityRoll = random.Next() < communityChance;
        bool marketLine = PrefersMarketLine(message) && CommunityAllowed(template, message) && MarketGroup(template, message, parent) != null;
        var chatterGroup = ChatterGroup(mind, template, message, parent);
        // 시세를 두고 하는 혼잣말은 번역하지 않는다. 그 언어로 따로 쓴 대사에서, 지금 상황과 말하는 사람의 처지에 맞는 것을 고른다
        // (ChatSituationSociety.cs). 그 언어의 대사 파일이 없을 때만 아래의 예전 길(한국어 원문 → 번역)로 간다.
        bool priceTalk = message.facts != null && (message.facts.notice == "WATCH_RISE" || message.facts.notice == "WATCH_FALL" ||
            message.facts.sourceEvent == "PriceRise" || message.facts.sourceEvent == "PriceFall");
        bool nativeCovers = false;
        if ((priceTalk || chatterGroup == null) && TryNativeMarketLine(mind, template, message, parent, recent, out string nativeLine, out nativeCovers)) { return nativeLine; }
        // 그 언어의 엔진이 맡는 말인데 엔진이 시세 얘기를 안 하기로 했으면(그 언어권이 이 상황을 잘 입에 올리지 않는다) 번역된 시세 대사로도 말하지 않는다.
        if (nativeCovers) { marketLine = false; }
        // 남의 말에 다는 답이고, 그 말의 갈래에 맞춰 쓴 답글 묶음이 있으면(자랑에 묻는 말, 한탄에 맞장구 …) 그것을 쓴다.
        // 예전에는 열에 몇 번만 썼고, 나머지는 방 전체에 하는 말투의 기본 문장이 나가서 "@아무개 혹시 OO 보는 사람 있어…?"가 됐다.
        SocialCommunityPhrases replyGroup = null;
        if (parent != null && CommunityAllowed(template, message))
        {
            replyGroup = ChatSocialAssets.Community(message.act, parent.act.ToString());
            if (replyGroup != null && !SpeechRequirementsMatch(replyGroup.requires, message.facts)) { replyGroup = null; }
        }
        if (communityRoll || marketLine || chatterGroup != null || replyGroup != null)
        {
            var group = replyGroup ?? chatterGroup ?? (marketLine ? MarketGroup(template, message, parent) : CommunityGroup(template, message, parent));
            if (group != null)
            {
                var candidates = new List<int>();
                for (int i = 0; i < group.texts.Length; i++)
                {
                    string key = SpeechChoiceKey(group, i);
                    bool repeated = mind.RecentSpeechKeys.Contains(key);
                    if (!repeated && recent != null)
                    { foreach (var previous in recent) { if (previous.debug?.speechKey == key) { repeated = true; break; } } }
                    if (repeated)
                    { continue; }
                    bool complete = true;
                    foreach (Match match in SlotPattern.Matches(group.texts[i]))
                    {
                        if (string.IsNullOrEmpty(SpeechSlot(message.facts, match.Groups[1].Value, target, 0)))
                        { complete = false; break; }
                    }
                    if (complete) { candidates.Add(i); }
                }
                if (candidates.Count > 0)
                {
                    int index = candidates[random.Range(0, candidates.Count)];
                    phrase = group.texts[index]; message.debug.speechKey = SpeechChoiceKey(group, index); community = true;
                }
            }
        }
        if (!community && mind.Traits.Serious && (message.facts.notice == "WATCH_RISE" || message.facts.notice == "WATCH_FALL" || message.facts.notice == "OPPONENT_LEFT" || IsMarketRiskNotice(message.facts.notice) || IsCompanyNews(message.facts.notice)))
        {
            string calmKey = template.key.Replace(".casual.", ".formal.").Replace(".rough.", ".formal.");
            foreach (var calm in ChatSocialAssets.Candidates(message.act, "formal", template.intensity, "plain"))
            {
                if (calm.key == calmKey) { phrase = calm.text; break; }
            }
        }
        string voiceText = null;
        bool voiced = !community && TryVoiceText(mind, template, message, parent, target, recent, out voiceText);
        if (voiced) { phrase = voiceText; }
        message.localizedPhrase = phrase;
        message.localizedTarget = message.targetId == 0 ? null : target;
        phrase = VoiceStockParticles(phrase, message.facts.stockName ?? message.facts.symbol);
        // Trading instructions retain exact limit prices, sizes and fill accounting. Only conversation P/L slots are abbreviated.
        bool colloquial = !message.facts.signal && (community || random.Next() < ChatSocialConfig.Current.Get(mind.Traits.Serious ? "seriousColloquialNumberChance" : "colloquialNumberChance"));
        int dialect = random.Range(0, 3);
        var localized = new List<SocialSpeechValue>();
        var localizedNames = new HashSet<string>();
        foreach (Match slot in SlotPattern.Matches(message.localizedPhrase))
        {
            string name = slot.Groups[1].Value;
            if (!localizedNames.Add(name)) { continue; }
            string raw = name == "gain" || name == "loss" ? message.facts.amount : name == "return" ? message.facts.pct :
                name == "moveTalk" ? message.facts.observationPercent : Slot(message.facts,name,target);
            localized.Add(new SocialSpeechValue { name = name, value = SpeechSlot(message.facts,name,target,dialect), raw = raw,
                approximate = colloquial && (name == "amount" || name == "memAmount" || name == "pct" || name == "memPct") });
        }
        message.localizedValues = localized.ToArray();
        message.debug.numberStyle = colloquial ? "구어체·근사 손익" : "정확한 손익";
        if (colloquial)
        {
            phrase = SpokenNumberPattern.Replace(phrase, match =>
            {
                string name = match.Groups[1].Value, value = Slot(message.facts, name, target);
                return name == "amount" || name == "memAmount" ? ChatSocialNumbers.Amount(value, 0, signed: true, compactNoun: true) : ChatSocialNumbers.Return(value, dialect);
            });
        }
        string text = SlotPattern.Replace(phrase, match => SpeechSlot(message.facts, match.Groups[1].Value, target, dialect));
        string decorated = community || voiced ? text : DecorateSocialText(mind, message, text);
        message.localizedSuffix = decorated.Length > text.Length ? decorated.Substring(text.Length) : "";
        return decorated;
    }
    private static string SpeechChoiceKey(SocialCommunityPhrases group, int index)
    {
        bool shared = group.context == "TARGET" && (group.act == "BRAG" || group.act == "LAMENT" || group.act == "ASK" || group.act == "REACT" || group.act == "SMALLTALK");
        return (shared ? group.key.Replace(".TARGET.", ".NONE.") : group.key) + "." + index;
    }
    /// <summary>약속·신호·기억·공지처럼 문장을 바꾸면 안 되는 말이 아닌가.</summary>
    private static bool CommunityAllowed(SocialTemplate template, SocialMessage message)
    {
        var facts = message.facts;
        return facts != null && !facts.signal && !message.memory && template.variant != "memory" && template.promisedAction == SocialPromisedAction.None &&
            string.IsNullOrEmpty(facts.notice) && template.key.Contains(".plain.");
    }
    private static SocialCommunityPhrases CommunityGroup(SocialTemplate template, SocialMessage message, SocialMessage parent)
    {
        var facts = message.facts;
        if (facts == null || facts.signal || message.memory || template.variant == "memory" || template.promisedAction != SocialPromisedAction.None)
        { return null; }
        bool watch = facts.notice == "WATCH_RISE" || facts.notice == "WATCH_FALL";
        bool fomo = facts.notice == "FOMO" && message.replyToId == 0;
        bool departure = facts.notice == "OPPONENT_LEFT" && !string.IsNullOrEmpty(facts.departedName);
        if (watch && (facts.observationBotId != message.senderId || message.replyToId != 0))
        { return null; }
        if (!watch && !departure && !fomo && (!string.IsNullOrEmpty(facts.notice) || !template.key.Contains(".plain.")))
        { return null; }
        string context = watch || departure || fomo ? facts.notice : parent != null ? parent.act.ToString() : message.targetId != 0 ? "TARGET" : "NONE";
        var group = ChatSocialAssets.Community(message.act, context);
        if (group == null && !watch && !departure && !fomo) { group = ChatSocialAssets.Community(message.act, parent != null || message.targetId != 0 ? "TARGET" : "NONE"); }
        return group != null && SpeechRequirementsMatch(group.requires, facts) ? group : null;
    }
    private static string SpeechSlot(SocialFacts facts, string slot, string target, int dialect) => slot switch
    {
        "gain" or "loss" => ChatSocialNumbers.Amount(facts.amount, dialect),
        "return" => ChatSocialNumbers.Return(facts.pct, dialect),
        "moveTalk" => ChatSocialNumbers.Return(facts.observationPercent, dialect),
        "positionState" => facts.profitKind == "실현" ? "팔고 보니까" : facts.held ? "아직 들고 있는데" : null,
        _ => Slot(facts, slot, target)
    };
}
