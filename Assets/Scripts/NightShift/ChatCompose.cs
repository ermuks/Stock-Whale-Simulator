using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 조합해서 만드는 말.
//
// 정해진 문장을 그대로 꺼내 쓰지 않는다. 그 봇의 지금 상태에서 "무슨 기분의 말을 할지"를 고르고, 그 기분에 맞는 낱말과 문장 틀을 짜 맞춘다:
//   1) 기분: 욕심·공포·스트레스·배신감, 그리고 들고 있는 종목이 수익인지 손실인지에서 정한다(들뜸, 불안, 지침, 억울함, 심심함, 수익 중, 물림, 우쭐함).
//   2) 서술어: 그 기분의 낱말 가운데 하나("불안함", "머리 아픔", "녹는 중임" …). 말끝은 그 봇의 버릇대로(~임 / ~다 / ~네).
//   3) 대상: 서술어가 받을 수 있는 갈래의 낱말만 붙는다. "녹는 중임"은 계좌에만, "수상함"은 종목과 호가창에만 붙는다.
//      서술어마다 "어떤 틀에서, 어떤 갈래의 대상과" 쓰이는지 적어 두었기 때문에 관계없는 것끼리 합쳐지지 않는다.
//   4) 틀: "{대상} {서술어}", "{대상} 때문에 {서술어}", "{대상} 덕분에 {서술어}", "{서술어}" 가운데 서술어가 허락한 것.
//   5) 꾸밈: 가끔 앞에 "진짜"·"오늘따라", 뒤에 "ㅋㅋ"·".."가 붙는다. 붙는 정도는 봇마다 다르다.
// 낱말은 언어마다 따로 쓴 파일(Chat/chat_compose.ko …)에서 온다. 파일이 없는 언어에서는 조합한 말을 하지 않는다.
//
// 오타: 아주 가끔 한 글자를 잘못 친다(받침을 빠뜨리거나, 쌍자음을 홑자음으로, ㅐ와 ㅔ를 바꿔서). 종목 이름·숫자·멘션은 건드리지 않는다.
//       그중 일부는 바로 다음 줄에 "*고친말"을 올린다.
[Serializable] public sealed class ComposeMood { public string id; public string polarity; public string[] predicates; }
[Serializable] public sealed class ComposeSubjects { public string kind; public string[] words; }
[Serializable] public sealed class ComposeFrames { public string relation; public string[] frames; }
[Serializable] public sealed class ComposeLocale
{
    public int schemaVersion = 1;
    public ComposeMood[] moods; public ComposeSubjects[] subjects; public ComposeFrames[] frames;
    public string[] intensifiers, tailsPositive, tailsNegative, tailsNeutral;
}

public static class ChatCompose
{
    private sealed class Predicate { public string[] forms; public readonly Dictionary<string, string[]> relations = new(StringComparer.Ordinal); }
    private sealed class Mood { public string polarity; public readonly List<Predicate> predicates = new(); }
    private sealed class Lexicon
    {
        public readonly Dictionary<string, Mood> moods = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string[]> subjects = new(StringComparer.Ordinal), frames = new(StringComparer.Ordinal);
        public string[] intensifiers, tailsPositive, tailsNegative, tailsNeutral;
    }
    private static Dictionary<string, Lexicon> lexicons;
    private static readonly string[] None = Array.Empty<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { lexicons = null; }

    private static Lexicon Current
    {
        get
        {
            if (lexicons == null)
            {
                lexicons = new Dictionary<string, Lexicon>(StringComparer.Ordinal);
                foreach (string code in ChatHypeAssets.Languages)
                {
                    var asset = Resources.Load<TextAsset>("Chat/chat_compose." + code);
                    var data = asset == null ? null : JsonUtility.FromJson<ComposeLocale>(asset.text);
                    if (data == null || data.schemaVersion != 1 || data.moods == null || data.frames == null || data.subjects == null) { continue; }
                    var lexicon = new Lexicon { intensifiers = data.intensifiers ?? None, tailsPositive = data.tailsPositive ?? None, tailsNegative = data.tailsNegative ?? None, tailsNeutral = data.tailsNeutral ?? None };
                    foreach (var group in data.subjects) { if (group != null && !string.IsNullOrEmpty(group.kind) && group.words != null) { lexicon.subjects[group.kind] = group.words; } }
                    foreach (var group in data.frames) { if (group != null && !string.IsNullOrEmpty(group.relation) && group.frames != null) { lexicon.frames[group.relation] = group.frames; } }
                    foreach (var mood in data.moods)
                    {
                        if (mood == null || string.IsNullOrEmpty(mood.id) || mood.predicates == null) { continue; }
                        var parsed = new Mood { polarity = mood.polarity ?? "neutral" };
                        foreach (string raw in mood.predicates)
                        {
                            // "말끝1/말끝2/말끝3#틀:갈래,갈래;틀:갈래;none"
                            int mark = raw.IndexOf('#'); if (mark <= 0) { continue; }
                            var predicate = new Predicate { forms = raw.Substring(0, mark).Split('/') };
                            foreach (string part in raw.Substring(mark + 1).Split(';'))
                            {
                                int colon = part.IndexOf(':');
                                predicate.relations[colon < 0 ? part.Trim() : part.Substring(0, colon).Trim()] = colon < 0 ? None : part.Substring(colon + 1).Split(',');
                            }
                            if (predicate.forms.Length > 0 && predicate.relations.Count > 0) { parsed.predicates.Add(predicate); }
                        }
                        if (parsed.predicates.Count > 0) { lexicon.moods[mood.id] = parsed; }
                    }
                    if (lexicon.moods.Count > 0) { lexicons[code] = lexicon; }
                }
            }
            return lexicons.TryGetValue(GameLocale.Code, out var found) ? found : null;
        }
    }
    public static bool Ready => Current != null;

    /// <summary>
    /// 그 기분의 한마디를 짜 맞춘다. stock은 "{stockName}"이 든 대상을 쓸 수 있는지(그 봇이 말할 종목이 있는지)를 정한다.
    /// holding: 그 종목을 들고 하는 말이면 종목 이름만 대상으로 쓴다("{stockName} 차트 물려 있음" 같은 말을 막는다).
    /// 짜 맞출 수 없으면 null.
    /// </summary>
    public static string Line(string mood, Func<double> random, int habit, float decoration, bool stock, bool holding)
    {
        var lexicon = Current;
        if (lexicon == null || !lexicon.moods.TryGetValue(mood, out var chosen)) { return null; }
        for (int tries = 0; tries < 8; tries++)
        {
            var predicate = chosen.predicates[(int)(random() * chosen.predicates.Count) % chosen.predicates.Count];
            // 이 서술어가 쓰일 수 있는 틀 가운데 하나.
            string relation = null; int seen = 0;
            foreach (var pair in predicate.relations)
            {
                if (!lexicon.frames.ContainsKey(pair.Key)) { continue; }
                if (pair.Value.Length > 0 && !AnyUsable(lexicon, pair.Value, stock)) { continue; }
                seen++; if ((int)(random() * seen) == 0) { relation = pair.Key; }
            }
            if (relation == null) { continue; }
            string subject = "";
            string[] kinds = predicate.relations[relation];
            if (kinds.Length > 0)
            {
                // 대상의 갈래를 고르고, 그 갈래의 낱말을 고른다. 종목이 없으면 종목 갈래는 쓰지 않는다.
                string kind = null; seen = 0;
                foreach (string candidate in kinds) { string name = candidate.Trim(); if (lexicon.subjects.ContainsKey(name) && (stock || !StockKind(name)) && !(holding && name == "view")) { seen++; if ((int)(random() * seen) == 0) { kind = name; } } }
                if (kind == null) { continue; }
                var words = lexicon.subjects[kind];
                subject = words[(int)(random() * words.Length) % words.Length];
            }
            var frames = lexicon.frames[relation];
            string frame = frames[(int)(random() * frames.Length) % frames.Length];
            string form = predicate.forms[Math.Abs(habit) % predicate.forms.Length];
            // 가끔은 버릇과 다른 말끝도 쓴다.
            if (random() < .25) { form = predicate.forms[(int)(random() * predicate.forms.Length) % predicate.forms.Length]; }
            string intensifier = lexicon.intensifiers.Length > 0 ? lexicon.intensifiers[(int)(random() * lexicon.intensifiers.Length) % lexicon.intensifiers.Length] : "";
            var tails = chosen.polarity == "positive" ? lexicon.tailsPositive : chosen.polarity == "negative" ? lexicon.tailsNegative : lexicon.tailsNeutral;
            // 꾸밈이 적은 봇은 꼬리를 거의 달지 않는다. 틀에 꼬리 자리가 있어도 비워 둔다.
            string tail = tails.Length > 0 && random() < decoration ? tails[(int)(random() * tails.Length) % tails.Length] : "";
            string line = frame.Replace("{S}", subject).Replace("{P}", form).Replace("{I}", intensifier).Replace("{T}", tail);
            if (frame.IndexOf("{T}", StringComparison.Ordinal) < 0 && tail.Length > 0 && random() < .5) { line += " " + tail; }
            line = Tidy(line);
            if (line.Length > 0) { return line; }
        }
        return null;
    }
    private static bool AnyUsable(Lexicon lexicon, string[] kinds, bool stock)
    {
        foreach (string kind in kinds) { string name = kind.Trim(); if (lexicon.subjects.ContainsKey(name) && (stock || !StockKind(name))) { return true; } }
        return false;
    }
    /// <summary>종목 이름이 들어가는 갈래인가(종목 그 자체, 또는 그 종목의 차트·호가).</summary>
    private static bool StockKind(string kind) => kind == "stock" || kind == "view";
    private static string Tidy(string line)
    {
        var result = new StringBuilder(line.Length); bool space = true;
        foreach (char c in line) { if (c == ' ') { if (!space) { result.Append(c); } space = true; } else { result.Append(c); space = false; } }
        return result.ToString().Trim();
    }

    // ── 오타 ──
    /// <summary>
    /// 한 글자를 잘못 친 글을 돌려준다. 고칠 낱말이 없으면 null. wrong·right에는 잘못 친 낱말과 원래 낱말이 담긴다(뒤이어 "*고친말"을 올릴 때 쓴다).
    /// 숫자·영문·기호가 섞인 낱말, 멘션, 지켜야 할 낱말(종목 이름)은 건드리지 않는다.
    /// </summary>
    public static string Typo(string text, Func<double> random, Func<string, bool> keep, out string wrong, out string right)
    {
        wrong = right = null;
        if (string.IsNullOrEmpty(text) || text.IndexOf('<') >= 0) { return null; }
        string[] words = text.Split(' ');
        int chosen = -1, seen = 0;
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (word.Length < 2 || word.Length > 7 || word[0] == '@' || !AllHangul(word) || keep != null && keep(word)) { continue; }
            seen++; if ((int)(random() * seen) == 0) { chosen = i; }
        }
        if (chosen < 0) { return null; }
        string original = words[chosen];
        for (int tries = 0; tries < 6; tries++)
        {
            int at = (int)(random() * original.Length) % original.Length;
            char slip = Slip(original[at], random);
            if (slip == original[at]) { continue; }
            var changed = original.ToCharArray(); changed[at] = slip;
            wrong = new string(changed); right = original; words[chosen] = wrong;
            return string.Join(" ", words);
        }
        return null;
    }
    private static bool AllHangul(string word)
    {
        foreach (char c in word) { if (c < '가' || c > '힣') { return false; } }
        return true;
    }
    /// <summary>한 글자를 흔히 틀리는 식으로 바꾼다: 받침 빠뜨리기, 겹받침·쌍자음을 홑자음으로, ㅐ와 ㅔ 바꾸기.</summary>
    private static char Slip(char syllable, Func<double> random)
    {
        int code = syllable - 0xAC00, initial = code / 588, vowel = code % 588 / 28, final = code % 28;
        int kind = (int)(random() * 4);
        if (kind == 0 && final != 0)
        {
            // 겹받침·쌍받침은 홑받침으로, 홑받침은 가끔 빠뜨린다. ㄲ→ㄱ, ㄳ→ㄱ, ㄵ·ㄶ→ㄴ, ㄺ·ㄻ·ㄼ·ㅀ→ㄹ, ㅄ→ㅂ, ㅆ→ㅅ
            int simple = final == 2 || final == 3 ? 1 : final == 5 || final == 6 ? 4 : final >= 9 && final <= 15 ? 8 : final == 18 ? 17 : final == 20 ? 19 : -1;
            final = simple >= 0 ? simple : random() < .35 ? 0 : final;
        }
        else if (kind == 1) { vowel = vowel == 1 ? 5 : vowel == 5 ? 1 : vowel == 3 ? 7 : vowel == 7 ? 3 : vowel; }   // ㅐ↔ㅔ, ㅒ↔ㅖ
        else if (kind == 2) { initial = initial == 1 ? 0 : initial == 4 ? 3 : initial == 8 ? 7 : initial == 10 ? 9 : initial == 13 ? 12 : initial; }   // 쌍자음 → 홑자음
        else if (kind == 3 && final == 0 && random() < .3) { final = 4; }   // 없던 ㄴ 받침이 붙는다(옆 글쇠)
        return (char)(0xAC00 + initial * 588 + vowel * 28 + final);
    }
}

public sealed partial class InvestorSociety
{
    /// <summary>한 줄에 오타가 날 확률, 오타를 낸 뒤 고쳐 올릴 확률.</summary>
    private const double TypoChance = .02, TypoFixChance = .35;
    private double moodNext;
    /// <summary>확인용: 조합해서 한 말, 오타의 수.</summary>
    public int ComposedLines { get; private set; }
    public int TyposMade { get; private set; }

    /// <summary>지금 그 봇의 기분. 들고 있는 종목의 손익이 뚜렷하면 그것이 먼저고, 아니면 욕심·공포·스트레스·배신감 가운데 가장 센 것.</summary>
    private string MoodOf(TradeBot bot, out Stock about, out bool holding)
    {
        about = null; holding = false;
        var state = bot.Social; var mind = state.Psychology;
        var held = BiggestHolding(bot);
        if (held != null && held.AcceptsOrders && HypeRandom.Next() < .45)
        {
            decimal average = bot.GetAvgPrice(held), result = average > 0m ? held.currentPrice / average - 1m : 0m;
            if (result >= .04m) { about = held; holding = true; return mind.Traits.Bravado >= 65 && HypeRandom.Next() < .4 ? "smug" : "win"; }
            if (result <= -.04m) { about = held; holding = true; return "lose"; }
        }
        // 가장 센 감정 하나. 다 밋밋하면 심심한 것이다.
        float up = mind.Greed - 55f, anxious = mind.Fear - 50f, stressed = state.Stress - 55f, bitter = mind.Betrayal - 35f;
        float most = Mathf.Max(Mathf.Max(up, anxious), Mathf.Max(stressed, bitter));
        string mood = most < 4f ? "bored" : most == bitter ? "bitter" : most == stressed ? "stressed" : most == anxious ? "anxious" : "up";
        if (mood != "bored" && mood != "bitter" && day.Phase == MarketPhase.Trading) { about = held != null && held.AcceptsOrders && HypeRandom.Next() < .5 ? held : DebateStock(); }
        return mood;
    }
    /// <summary>
    /// 토론도 급등락도 없는 조용한 때, 누군가 지금 기분을 한마디 한다. 그 말은 그 봇의 상태에서 조합해 만든다.
    /// 10~22초에 한 번. 조합할 낱말 파일이 없는 언어에서는 하지 않는다.
    /// </summary>
    private void TickMoodTalk()
    {
        if (day == null || !SocialReady || !ChatCompose.Ready || !ChatHypeAssets.Ready || StockManager.Instance == null) { return; }
        double now = SocialTime.Seconds;
        if (moodNext <= 0 || moodNext - now > 300) { moodNext = now + 8 + HypeRandom.Next() * 10; }
        if (now < moodNext || hypeRun != null) { return; }
        moodNext = now + (debate != null ? 18 : 10) + HypeRandom.Next() * 12;
        debateFolk.Clear(); float clock = SessionClock.Time; var odd = ResolveOddball(); int formal = Config.Int("styleFormalMin");
        foreach (var candidate in socialBots)
        { if (DebateEligible(candidate, clock, odd, formal) && (debate == null || !debate.cast.Contains(candidate.Social.Id)) && (!hypeSpokeAt.TryGetValue(candidate.Social.Id, out double spoke) || now - spoke > 40)) { debateFolk.Add(candidate); } }
        if (debateFolk.Count == 0) { return; }
        var bot = debateFolk[HypeRandom.Range(0, debateFolk.Count)];
        string mood = MoodOf(bot, out Stock about, out bool holding);
        // 말끝 버릇과 꾸밈의 정도는 봇마다 정해져 있다.
        int habit = (int)(CompanyBusiness.Hash("ENDING:" + bot.Social.Id) % 3u);
        float decoration = .15f + (CompanyBusiness.Hash("DECOR:" + bot.Social.Id) % 100u) / 100f * .55f;
        // 낱말을 고르고 짜 맞추는 일은 작업 스레드가 한다. 여기서는 재료(기분, 말버릇, 빈칸에 넣을 값)만 담아 맡긴다.
        var slots = new Dictionary<string, string>(about != null ? ChartSlots(about, about.currentPrice, about.currentPrice, bot.GetAvgPrice(about)) : HypeSlots(null, 0, 0m));
        bool hasStock = about != null; string nickname = bot.Social.Nickname;
        string feelings = "욕심 " + bot.Social.Psychology.Greed.ToString("0") + " 공포 " + bot.Social.Psychology.Fear.ToString("0") + " 스트레스 " + bot.Social.Stress.ToString("0");
        QueueCompose(bot, DebateRoom, random =>
        {
            string line = ChatCompose.Line(mood, random.NextDouble, habit, decoration, hasStock, holding);
            return line == null ? null : ChatHypeAssets.Fill(line, slots);
        }, "mood." + mood, about, 0, "MOOD", now, 0, said => { ComposedLines++; TraceDebate("    ~ " + nickname + ": " + said + "  [기분 " + mood + " · " + feelings + "]"); });
        hypeSpokeAt[bot.Social.Id] = now;
    }

}
