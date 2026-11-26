using System;
using System.Collections.Generic;
using UnityEngine;

// 잔움직임에 대한 반응.
//
// 급등·급락 같은 큰일에는 따로 떠드는 장치가 있다(ChatSocialHype). 여기는 그보다 훨씬 작은 움직임을 본다:
//   - 몇 초 사이에 1%쯤 툭 오르면 누군가 "쏘나?" 한다(pop).
//   - 오르던 종목이 몇 초 사이에 1%쯤 밀리면 누군가 "쏟는다" 한다(spill).
// 같은 움직임을 보고도 사람마다 다르게 말한다. 귀가 얇고 들떠 있는(또는 겁먹은) 봇은 호들갑을 떨고, 무던한 봇은 심드렁하게 한마디 한다.
// 의심하는 봇, 농담하는 봇도 있고, 그 종목을 들고 있느냐에 따라서도 말이 갈린다(들고 있으면 기대·버티기, 없으면 부러움·구경).
// 말투의 갈래(tone)는 fuss(호들갑) · dry(무덤덤) · doubt(의심) · joke(농담) · holder(보유자) · fomo/outsider(미보유자).
// 문장은 언어마다 따로 쓴 파일(Chat/chat_twitch.ko …)에서 온다. 문장을 고르고 빈칸을 채우는 일은 작업 스레드가 한다(ChatScheduler).
[Serializable] public sealed class TwitchGroup { public string key; public string[] lines; }
[Serializable] public sealed class TwitchLocale { public int schemaVersion = 1; public TwitchGroup[] groups; }

public static class ChatTwitchAssets
{
    private static Dictionary<string, Dictionary<string, string[]>> locales;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { locales = null; }
    private static Dictionary<string, string[]> Current
    {
        get
        {
            if (locales == null)
            {
                locales = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.Ordinal);
                foreach (string code in ChatHypeAssets.Languages)
                {
                    var asset = Resources.Load<TextAsset>("Chat/chat_twitch." + code);
                    var data = asset == null ? null : JsonUtility.FromJson<TwitchLocale>(asset.text);
                    if (data == null || data.schemaVersion != 1 || data.groups == null) { continue; }
                    var groups = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    foreach (var group in data.groups) { if (group != null && !string.IsNullOrEmpty(group.key) && group.lines != null && group.lines.Length > 0) { groups[group.key] = group.lines; } }
                    if (groups.Count > 0) { locales[code] = groups; }
                }
            }
            return locales.TryGetValue(GameLocale.Code, out var current) ? current : null;
        }
    }
    /// <summary>지금 언어의 문장 파일이 있는가. 없으면 이 반응은 하지 않는다.</summary>
    public static bool Ready => Current != null;
    public static string[] Lines(string key) => Current != null && Current.TryGetValue(key, out var lines) ? lines : null;
}

public sealed partial class InvestorSociety
{
    /// <summary>몇 초 사이의 움직임이 이만큼이면 한마디 한다. 이보다 훨씬 크면 급등·급락 쪽이 맡는다.</summary>
    private const decimal TwitchMove = .008m, TwitchTooBig = .05m, TwitchPriorRise = .015m, TwitchDayRise = .04m;
    private const double TwitchLookSec = 6, TwitchTrendSec = 40;
    private double twitchCheck, twitchNext;
    private readonly Dictionary<Stock, double> twitchRest = new();
    private readonly List<string> twitchUsed = new();
    private readonly List<(string tone, double weight)> twitchTones = new();
    /// <summary>확인용: 잔움직임에 한 말의 수.</summary>
    public int TwitchLines { get; private set; }

    private void TickTwitch()
    {
        if (day == null || !SocialReady || day.Phase != MarketPhase.Trading || StockManager.Instance == null || !ChatHypeAssets.Ready || !ChatTwitchAssets.Ready) { return; }
        double now = SocialTime.Seconds;
        // 시계가 뒤로 갔으면(불러오기) 쉬는 시간을 잊는다.
        if (twitchCheck - now > 60 || twitchNext - now > 120) { twitchCheck = twitchNext = 0; twitchRest.Clear(); }
        if (now < twitchCheck) { return; }
        twitchCheck = now + 2;
        if (now < twitchNext) { return; }
        Stock found = null; string kind = null; decimal move = 0m;
        foreach (var pair in chartTracks)
        {
            var stock = pair.Key; var track = pair.Value;
            if (stock == null || !stock.AcceptsOrders || stock.currentPrice <= 0m || hypeRun != null && hypeRun.stock == stock) { continue; }
            if (twitchRest.TryGetValue(stock, out double rest) && now < rest) { continue; }
            if (!PriceAgo(track, now, TwitchLookSec, out decimal before) || before <= 0m) { continue; }
            decimal recent = stock.currentPrice / before - 1m;
            if (Math.Abs(recent) < TwitchMove || Math.Abs(recent) >= TwitchTooBig || Math.Abs(recent) <= Math.Abs(move)) { continue; }
            if (recent > 0m) { found = stock; kind = "pop"; move = recent; continue; }
            // 밀리는 쪽은 "오르던 종목"일 때만 호들갑거리가 된다: 방금까지 오르고 있었거나, 오늘 크게 올라 있다.
            decimal trend = PriceAgo(track, now, TwitchTrendSec, out decimal earlier) && earlier > 0m ? before / earlier - 1m : 0m, dayMove = 0m;
            if (ReadMarketDay != null) { var board = ReadMarketDay(stock); if (board.reference > 0m) { dayMove = stock.currentPrice / board.reference - 1m; } }
            if (trend >= TwitchPriorRise || dayMove >= TwitchDayRise) { found = stock; kind = "spill"; move = recent; }
        }
        if (found == null) { return; }
        twitchRest[found] = now + 16 + HypeRandom.Next() * 18;
        // 같은 1%라도 하루 전체에서 무엇인지에 따라 말이 달라진다(ChatSituationRules.Twitch):
        //   크게 오른 종목이 살짝 밀린 것 → 느긋한 말만("숨 고르네"). 예전에는 오늘 +4%만 넘으면 "끝났다"까지 나왔다.
        //   무너지는 종목이 살짝 밀린 것 → 따로 말하지 않는다. 무너지는 종목이 살짝 튄 것 → 시큰둥하거나 의심하는 말만.
        var read = SituationOf(found);
        var mode = read.valid ? ChatSituationRules.Twitch(kind == "pop", read) : ChatSituationRules.TwitchMode.Normal;
        if (mode == ChatSituationRules.TwitchMode.Silent) { return; }
        // 호들갑이 아닌 말은 그 언어권이 이 상황을 입에 올리는 만큼만 한다(조용히 보는 사람이 많은 언어권은 그냥 지나간다).
        var engine = ChatReactionAssets.Current;
        if (mode != ChatSituationRules.TwitchMode.Normal && engine != null && HypeRandom.Next() >= engine.TalkChance(read.kind, ChatStance.Watch)) { return; }
        twitchNext = now + 5 + HypeRandom.Next() * 8;
        debateFolk.Clear(); float clock = SessionClock.Time; var odd = ResolveOddball(); int formal = Config.Int("styleFormalMin");
        foreach (var candidate in socialBots)
        { if (DebateEligible(candidate, clock, odd, formal) && (!hypeSpokeAt.TryGetValue(candidate.Social.Id, out double spoke) || now - spoke > 25)) { debateFolk.Add(candidate); } }
        // 한 사람만 툭 던질 때도 있고, 두세 사람이 저마다 다른 투로 한마디씩 할 때도 있다. 느긋한 얘기에는 여럿이 달려들지 않는다.
        int speakers = mode == ChatSituationRules.TwitchMode.Normal ? 1 + (HypeRandom.Next() < .55 ? 1 : 0) + (HypeRandom.Next() < .22 ? 1 : 0) : 1 + (HypeRandom.Next() < .3 ? 1 : 0);
        double delay = 0;
        string moved = Math.Abs(move * 100m).ToString("0.0", SocialCulture);
        for (int n = 0; n < speakers && debateFolk.Count > 0; n++)
        {
            int index = HypeRandom.Range(0, debateFolk.Count); var bot = debateFolk[index]; debateFolk.RemoveAt(index);
            bool holding = bot.GetOwnedVolume(found) > 0m;
            string tone = TwitchTone(bot, kind, holding, mode, holding ? BotView(bot, found, out _) : ChatSpeakerView.Watcher);
            // 보유자의 말은 수익 중인 사람과 물린 사람이 다르다. 그렇게 나눠 쓴 묶음이 없는 언어에서는 예전의 한 묶음(holder)을 쓴다.
            var pool = ChatTwitchAssets.Lines(kind + "." + tone) ?? (tone.StartsWith("holder", StringComparison.Ordinal) ? ChatTwitchAssets.Lines(kind + ".holder") : null) ?? ChatTwitchAssets.Lines(kind + ".dry");
            if (pool == null) { continue; }
            // 방금 나온 문장은 빼고 고르게 한다(다 빠졌으면 그대로 둔다).
            var fresh = new List<string>(pool.Length);
            foreach (string line in pool) { if (!twitchUsed.Contains(line)) { fresh.Add(line); } }
            string[] choices = fresh.Count > 0 ? fresh.ToArray() : pool;
            var slots = new Dictionary<string, string>(ChartSlots(found, found.currentPrice, found.currentPrice, bot.GetAvgPrice(found))) { ["move"] = moved };
            string nickname = bot.Social.Nickname, label = kind + "." + tone + (holding ? ", 보유" : ", 미보유"), picked = null;
            // 말맛(ChatVoice.cs): 잔움직임은 잔잔한 일이다. 호들갑·조바심만 한 단 올려 치고(세기 1), 나머지는 가장 낮게 친다(세기 0).
            // 규칙과 버릇은 여기(메인 스레드)에서 읽어 두고, 입히는 일은 작업 스레드가 한다(ChatVoice는 넘겨받은 것만 쓴다).
            var voice = ChatReactionAssets.Voice(GameLocale.Code); uint habit = SpeechHabit(bot.Social.Id);
            int level = tone == "fuss" || tone == "fomo" ? ChatVoice.Warm : ChatVoice.Mild;
            var lean = tone == "calm" || tone == "dry" ? ChatVoiceLean.Calm : kind == "pop" ? ChatVoiceLean.Up : ChatVoiceLean.Down;
            QueueCompose(bot, DebateRoom, random =>
            {
                picked = choices[random.Next(choices.Length)];
                return ChatHypeAssets.Fill(ChatVoice.Apply(picked, voice, level, lean, habit, random.NextDouble), slots);
            }, "twitch." + kind + "." + tone, found, 0, "TWITCH", now, delay, said =>
            {
                TwitchLines++;
                if (picked != null) { twitchUsed.Add(picked); while (twitchUsed.Count > 36) { twitchUsed.RemoveAt(0); } }
                TraceDebate("    ^ " + nickname + ": " + said + "  [" + label + " · " + (kind == "pop" ? "+" : "-") + moved + "%]");
            });
            hypeSpokeAt[bot.Social.Id] = now;
            delay += 1.2 + HypeRandom.Next() * 2.6;
        }
    }
    /// <summary>이 봇은 이 움직임을 어떤 투로 말할까. 귀가 얇고 들떠 있을수록(밀릴 때는 겁먹었을수록) 호들갑, 무던할수록 심드렁하다.</summary>
    /// <remarks>
    /// mode가 Calm이면(크게 오른 종목의 잔밀림) 호들갑·의심은 빠지고 느긋한 투(calm)가 들어온다.
    /// Wary면(무너지는 종목의 잔반등) 호들갑·조바심이 빠진다. 보유자의 투는 처지에 따라 holderGain·holderLoss로 갈린다.
    /// </remarks>
    private string TwitchTone(TradeBot bot, string kind, bool holding, ChatSituationRules.TwitchMode mode, ChatSpeakerView view)
    {
        var mind = bot.Social.Psychology;
        float feeling = kind == "pop" ? mind.Greed : mind.Fear;
        double excited = Math.Max(0, Math.Min(1, (mind.Traits.Suggestibility - 30) / 60d * .6 + (feeling - 40f) / 50d * .4));
        bool normal = mode == ChatSituationRules.TwitchMode.Normal;
        string holder = view == ChatSpeakerView.Loss || view == ChatSpeakerView.DeepLoss ? "holderLoss" : view == ChatSpeakerView.Even ? "holder" : "holderGain";
        twitchTones.Clear();
        if (normal) { twitchTones.Add(("fuss", .45 + excited * .8)); }
        else if (mode == ChatSituationRules.TwitchMode.Calm) { twitchTones.Add(("calm", .9)); }
        twitchTones.Add(("dry", .3 + (1 - excited) * .5));
        if (normal || mode == ChatSituationRules.TwitchMode.Wary) { twitchTones.Add(("doubt", normal ? .35 : .6)); }
        twitchTones.Add(("joke", .2 + mind.Traits.Bravado / 100d * .2));
        // 크게 수익 중인 사람은 크게 오른 종목의 잔밀림에 "내 수익 녹는다"고 하지 않는다. 느긋한 말이나 농담을 한다.
        bool relaxed = mode == ChatSituationRules.TwitchMode.Calm && view == ChatSpeakerView.DeepProfit;
        if (holding) { if (!relaxed) { twitchTones.Add((holder, kind == "pop" ? .45 : .55)); } }
        else if (kind != "pop") { twitchTones.Add(("outsider", .35)); }
        else if (normal) { twitchTones.Add(("fomo", .25 + excited * .4)); }
        double total = 0; foreach (var tone in twitchTones) { total += tone.weight; }
        double roll = HypeRandom.Next() * total;
        foreach (var tone in twitchTones) { roll -= tone.weight; if (roll <= 0) { return tone.tone; } }
        return "dry";
    }
}
