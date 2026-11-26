using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// 시세 반응 엔진. 언어마다 하나씩 있다.
//
// 상황 판정(ChatSituation.cs)은 모든 언어가 같이 쓴다. 그 뒤는 언어마다 다르다. 번역이 아니라 따로 만든 것이다:
//   한국어(ChatReactionKo)  주식 커뮤니티 말투. 반말, 짧고 빠른 호들갑과 냉소. 한 사건에 두세 명이 연달아 친다.
//                           숫자는 "490퍼", "6배". 말끝에 ㅋㅋ·ㄷㄷ·..가 붙는 버릇이 봇마다 다르다.
//   영어(ChatReactionEn)    WSB·핀트윗 말투. 마른 비꼼. 한두 명이 조금 길게 말한다.
//                           숫자는 "490%", "5.9x", "5-bagger". 전부 소문자로 치고 마침표를 안 찍는 사람이 섞여 있다.
//   일본어(ChatReactionJa)  5ch·株クラ 말투. 덤덤한 비꼼, 느낌표는 거의 없다. 조용히 보는 사람이 많아 말수가 가장 적다.
//                           숫자는 "+490%", "6倍". 말끝에 ｗ·草가 붙는 버릇(전각 ｗ를 쓰는 사람과 반각 w를 쓰는 사람)이 있다.
// 무엇이 다른가는 되도록 파일(Chat/chat_market.ko/en/ja)에 적는다: 어떤 상황을 얼마나 입에 올리는지(talk), 처지에 따른 말수(stanceTalk),
// 숫자가 든 문장을 고르는 비율, 한 사건에 말하는 사람 수, 말끝 버릇, 숫자 표기. 대사는 상황 × 처지(gain·loss·watch, 크게 수익 중이면 deep)로 묶여 있다.
// 그 언어의 파일이 없으면 엔진도 없다. 그때는 예전 길(한국어 원문을 번역한 대사)로 돌아간다.

[Serializable] public sealed class ChatMarketGroup { public string key; public string[] texts; }
[Serializable] public sealed class ChatMarketNumbers
{
    /// <summary>등락률을 말로 적는 틀. {n}에 숫자가 들어간다("{n}퍼", "{n}%", "+{n}%").</summary>
    public string percentUp = "+{n}%", percentDown = "-{n}%";
    /// <summary>기준가의 몇 배인지 적는 틀("{n}배", "{n}x", "{n}倍").</summary>
    public string multiple = "{n}x";
    /// <summary>"5-bagger"처럼 배수를 명사로 부르는 틀. 그런 말이 없는 언어는 비워 둔다.</summary>
    public string bagger = "";
    /// <summary>배수가 이 값 이상이면 정수로만 말한다("6배"). 그 아래는 소수 한 자리("2.5배").</summary>
    public double wholeMultipleFrom = 3;
}
[Serializable] public sealed class ChatMarketStyle
{
    /// <summary>숫자가 든 문장과 없는 문장이 다 있을 때 숫자가 든 쪽을 고를 확률. 사람은 대개 숫자 없이 말한다.</summary>
    public double numberLineChance = .3;
    /// <summary>한 사건에 몇 사람이 한마디씩 하는가.</summary>
    public int burstMin = 1, burstMax = 2;
    /// <summary>말끝 버릇이 붙을 확률과 붙는 말(오를 때, 내릴 때, 덤덤할 때).</summary>
    public double tailChance;
    public string[] tailsUp, tailsDown, tailsCalm;
    /// <summary>전부 소문자로 치는 사람의 비율, 마침표를 안 찍는 사람의 비율(영어).</summary>
    public double lowercaseShare, dropPeriodShare;
    /// <summary>크게 수익 중인 보유자가 자기만의 대사(deep)를 고를 확률.</summary>
    public double deepChance = .6;
}
[Serializable] public sealed class ChatMarketLocale
{
    public int schemaVersion = 1;
    public ChatMarketNumbers numbers; public ChatMarketStyle style;
    /// <summary>말맛 규칙(ChatVoice.cs): ㅋ·ㄷ 줄의 길이, 말머리, 끼우는 말, 맞장구. 없으면 파일에 적힌 그대로 말한다.</summary>
    public ChatVoiceRules voice;
    /// <summary>상황마다 이 언어권이 입에 올릴 확률(0~1). 없는 상황은 1.</summary>
    public SocialWeight[] talk;
    /// <summary>처지(gain·loss·watch)에 따른 말수의 배율. 없으면 1.</summary>
    public SocialWeight[] stanceTalk;
    public ChatMarketGroup[] groups;
}

/// <summary>한 언어의 시세 반응 엔진. 공통 뼈대이고, 숫자 표기와 말끝 버릇은 언어별 클래스가 맡는다.</summary>
public abstract class ChatReactionEngine
{
    protected static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly string[] None = Array.Empty<string>();
    /// <summary>숫자가 들어가는 빈칸. 이런 빈칸이 든 문장은 가끔만 고른다.</summary>
    private static readonly string[] NumberSlots = { "{dayPct}", "{dayTalk}", "{multTalk}", "{bagger}", "{fromHigh}", "{fromLow}", "{retrace}", "{gainLeft}", "{price}", "{dayHigh}", "{dayLow}" };
    public readonly string Code;
    protected readonly ChatMarketLocale Data;
    private readonly Dictionary<string, string[]> index = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> talk = new(StringComparer.Ordinal), stanceTalk = new(StringComparer.Ordinal);
    private readonly List<string> plain = new(), numbered = new();

    protected ChatReactionEngine(string code, ChatMarketLocale data)
    {
        Code = code; Data = data;
        data.numbers ??= new ChatMarketNumbers(); data.style ??= new ChatMarketStyle();
        foreach (var group in data.groups) { index[group.key] = group.texts; }
        if (data.talk != null) { foreach (var row in data.talk) { if (row != null && !string.IsNullOrEmpty(row.key)) { talk[row.key] = row.value; } } }
        if (data.stanceTalk != null) { foreach (var row in data.stanceTalk) { if (row != null && !string.IsNullOrEmpty(row.key)) { stanceTalk[row.key] = row.value; } } }
    }

    public ChatMarketStyle Style => Data.style;
    /// <summary>이 언어의 말맛 규칙. 파일에 없으면 null(그때는 파일에 적힌 그대로 말한다).</summary>
    public ChatVoiceRules Voice => Data.voice;
    /// <summary>이 언어의 말끝 버릇 모두(오를 때·내릴 때·덤덤할 때). 점검 도구가 겹친 말끝을 찾을 때 쓴다.</summary>
    public IEnumerable<string> Tails
    {
        get
        {
            foreach (var list in new[] { Data.style.tailsUp, Data.style.tailsDown, Data.style.tailsCalm }) { if (list != null) { foreach (string tail in list) { yield return tail; } } }
        }
    }
    public IEnumerable<string> Keys => index.Keys;
    public string[] Lines(string key) => index.TryGetValue(key, out var texts) ? texts : None;
    public bool Has(ChatSituation kind, ChatStance stance) => index.ContainsKey(kind + "." + ChatSituationMath.StanceKey(stance));

    /// <summary>이 상황을 이 처지의 사람이 입에 올릴 확률. 말할 거리가 생겼을 때 굴린다(침묵도 반응이다).</summary>
    public double TalkChance(ChatSituation kind, ChatStance stance)
    {
        double chance = talk.TryGetValue(kind.ToString(), out double value) ? value : 1;
        if (stanceTalk.TryGetValue(ChatSituationMath.StanceKey(stance), out double scale)) { chance *= scale; }
        return Math.Max(0, Math.Min(1, chance));
    }
    /// <summary>한 사건에 말할 사람 수. offered는 그 사건이 원래 부르던 수(차트 수다의 줄 수)다.</summary>
    public int BurstLines(int offered, double roll)
    {
        int min = Math.Max(1, Data.style.burstMin), max = Math.Max(min, Data.style.burstMax);
        return Math.Max(1, Math.Min(offered, min + (int)(roll * (max - min + 1))));
    }

    /// <summary>
    /// 말에 넣을 수 있는 수치를 빈칸에 채운다. 말이 안 되는 값은 넣지 않는다(고점에 붙어 있는데 "고점 대비"를 말하지 않는다).
    ///   {dayPct} 전일 대비, 부호와 % 포함("+490%") · {fromHigh} 고점 대비("-2%") · {fromLow} 저점 대비("+8%")
    ///   {retrace} 오른 폭 가운데 돌려준 몫("15%") · {gainLeft} 남은 몫("85%") · {dayHigh} {dayLow} 당일 고가·저가
    ///   {dayTalk} {multTalk} {bagger} 는 그 언어의 말투로 적은 등락률과 배수.
    /// </summary>
    public void FillSlots(in ChatSituationRead read, IDictionary<string, string> slots)
    {
        if (!read.valid) { return; }
        slots["dayPct"] = ChatSituationMath.SignedPercent(read.dayPct);
        if (read.fromHigh <= -1m) { slots["fromHigh"] = ChatSituationMath.SignedPercent(read.fromHigh); } else { slots.Remove("fromHigh"); }
        if (read.fromLow >= 1m) { slots["fromLow"] = ChatSituationMath.SignedPercent(read.fromLow); } else { slots.Remove("fromLow"); }
        if (read.highPct >= ChatSituationMath.NotableGain && read.retrace >= .05m && read.retrace <= 1m)
        { slots["retrace"] = ChatSituationMath.SharePercent(read.retrace); slots["gainLeft"] = ChatSituationMath.SharePercent(1m - read.retrace); }
        else { slots.Remove("retrace"); slots.Remove("gainLeft"); }
        slots["dayHigh"] = InvestorSociety.Money(read.high); slots["dayLow"] = InvestorSociety.Money(read.low);
        slots.Remove("dayTalk"); slots.Remove("multTalk"); slots.Remove("bagger");
        Numbers(read, slots);
    }
    /// <summary>그 언어의 숫자 말투. 등락률이 1%도 안 되면 숫자로 말하지 않고, 배수는 두 배 이상 오른 날에만 말한다.</summary>
    protected abstract void Numbers(in ChatSituationRead read, IDictionary<string, string> slots);
    /// <summary>그 언어의 말끝 버릇. habit은 봇마다 정해진 값이다. 빈칸({…})은 건드리지 않는다.</summary>
    public abstract string Dress(string line, ChatSituation kind, uint habit, Func<double> random);

    protected string PercentTalk(decimal dayPct, int decimalsBelowTen)
    {
        decimal size = Math.Abs(dayPct);
        if (size < 1m) { return null; }
        string number = Math.Round(size, size < 10m ? decimalsBelowTen : 0, MidpointRounding.AwayFromZero).ToString("0.#", Culture);
        return (dayPct >= 0m ? Data.numbers.percentUp : Data.numbers.percentDown).Replace("{n}", number);
    }
    protected string MultipleNumber(decimal multiple, bool floor)
    {
        if (multiple >= (decimal)Data.numbers.wholeMultipleFrom) { return (floor ? Math.Floor(multiple) : Math.Round(multiple, MidpointRounding.AwayFromZero)).ToString("0", Culture); }
        return (Math.Floor(multiple * 10m) / 10m).ToString("0.#", Culture);
    }
    protected static bool EndsWithAny(string line, string marks)
        => line.Length > 0 && marks.IndexOf(line[line.Length - 1]) >= 0;
    /// <summary>붙이려는 말끝이 늘여 치는 글자(ㅋ·ㄷ·ㅠ·ｗ)이고, 그 글자가 문장에 이미 들어 있는가.</summary>
    protected bool RunAlready(string line, string tail)
        => tail.Length > 0 && Voice != null && !string.IsNullOrEmpty(Voice.runChars) && Voice.runChars.IndexOf(tail[0]) >= 0 && line.IndexOf(tail[0]) >= 0;
    /// <summary>문장에 그 낱말이 따로 서 있는가("ngl"로 시작한 말에 "ngl"을 또 달지 않는다). 띄어 쓰는 언어용.</summary>
    protected static bool HasWord(string line, string word)
    {
        if (string.IsNullOrEmpty(word)) { return false; }
        for (int at = line.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0; at = line.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            int end = at + word.Length;
            if ((at == 0 || !char.IsLetterOrDigit(line[at - 1])) && (end >= line.Length || !char.IsLetterOrDigit(line[end]))) { return true; }
        }
        return false;
    }
    protected string Tail(ChatSituation kind, Func<double> random)
    {
        var style = Data.style;
        var tails = ChatSituationRules.Rising(kind) || kind == ChatSituation.Recovery ? style.tailsUp :
            ChatSituationRules.Falling(kind) || kind == ChatSituation.Pullback || kind == ChatSituation.DeadCatBounce ? style.tailsDown : style.tailsCalm;
        return tails == null || tails.Length == 0 ? null : tails[(int)(random() * tails.Length) % tails.Length];
    }

    /// <summary>문장의 빈칸이 모두 채워질 수 있는가.</summary>
    public static bool Fillable(string line, IReadOnlyDictionary<string, string> slots)
    {
        for (int open = line.IndexOf('{'); open >= 0; open = line.IndexOf('{', open + 1))
        {
            int close = line.IndexOf('}', open + 1);
            if (close < 0) { return true; }
            if (!slots.TryGetValue(line.Substring(open + 1, close - open - 1), out string value) || string.IsNullOrEmpty(value)) { return false; }
            open = close;
        }
        return true;
    }
    private static bool Numbered(string line)
    {
        foreach (string slot in NumberSlots) { if (line.IndexOf(slot, StringComparison.Ordinal) >= 0) { return true; } }
        return false;
    }

    /// <summary>
    /// 이 상황에서 이 처지의 사람이 할 한마디(빈칸은 아직 안 채운 틀). 없으면 null.
    /// recent: 방금 나온 문장인가(그런 문장은 피한다). key는 고른 묶음의 이름("market.MonsterDip.gain"), index는 그 안의 번호.
    /// 메인 스레드에서만 부른다.
    /// </summary>
    public string Pick(ChatSituation kind, ChatSpeakerView view, ChatStance stance, IReadOnlyDictionary<string, string> slots, Func<double> random, Func<string, bool> recent, out string key, out int number)
    {
        string name = kind + "." + ChatSituationMath.StanceKey(stance);
        // 크게 수익 중인 사람은 따로 쓴 대사가 있으면 주로 그쪽에서 고른다(눌림에 한탄하지 않고 으쓱하거나 무덤덤하다).
        if (view == ChatSpeakerView.DeepProfit && index.ContainsKey(kind + ".deep") && random() < Data.style.deepChance) { name = kind + ".deep"; }
        key = "market." + name; number = -1;
        if (!index.TryGetValue(name, out var pool)) { return null; }
        for (int pass = 0; pass < 2; pass++)
        {
            plain.Clear(); numbered.Clear();
            foreach (string line in pool)
            {
                if (!Fillable(line, slots) || pass == 0 && recent != null && recent(line)) { continue; }
                (Numbered(line) ? numbered : plain).Add(line);
            }
            if (plain.Count + numbered.Count > 0) { break; }
        }
        if (plain.Count + numbered.Count == 0) { return null; }
        var from = numbered.Count > 0 && (plain.Count == 0 || random() < Data.style.numberLineChance) ? numbered : plain;
        string chosen = from[(int)(random() * from.Count) % from.Count];
        number = Array.IndexOf(pool, chosen);
        return chosen;
    }
}

/// <summary>한국어: 커뮤니티 말투. "490퍼", "6배". 말끝에 ㅋㅋ·ㄷㄷ·..를 붙이는 사람, 가끔 붙이는 사람, 안 붙이는 사람이 있다.</summary>
public sealed class ChatReactionKo : ChatReactionEngine
{
    public ChatReactionKo(ChatMarketLocale data) : base("ko", data) { }
    protected override void Numbers(in ChatSituationRead read, IDictionary<string, string> slots)
    {
        // 소수는 말하지 않는다("3.2퍼"가 아니라 "3퍼").
        string day = PercentTalk(read.dayPct, 0);
        if (day != null) { slots["dayTalk"] = day; }
        if (read.multiple >= 2m) { slots["multTalk"] = Data.numbers.multiple.Replace("{n}", MultipleNumber(read.multiple, false)); }
    }
    public override string Dress(string line, ChatSituation kind, uint habit, Func<double> random)
    {
        // 말맛(ChatVoice.cs): 가끔 "와"·"아니"로 말을 열고 "진짜"·"ㄹㅇ"을 끼운다. 얼마나 요란한지는 상황의 세기가 정한다.
        int level = ChatVoice.Level(Voice, kind);
        line = ChatVoice.Before(line, Voice, level, ChatVoice.Lean(kind), habit, random);
        // 버릇: 셋 중 하나는 꼬리를 안 달고, 하나는 가끔, 하나는 자주 단다. 이미 ㅋ·ㄷ·ㅠ·문장부호로 끝난 말에는 덧붙이지 않는다.
        uint kindOf = habit % 3u;
        if (kindOf != 0u && !EndsWithAny(line, "ㅋㄷㅠㅎ.?!~;…}") && random() < Data.style.tailChance * (kindOf == 2u ? 2 : 1))
        {
            string tail = Tail(kind, random);
            // 이미 ㅋㅋ가 든 말에 ㅋㅋ를 또 달지 않는다("490퍼 ㅋㅋㅋ 미쳤네 ㅋㅋ").
            if (tail != null && !RunAlready(line, tail)) { line = tail.StartsWith(".", StringComparison.Ordinal) ? line + tail : line + " " + tail; }
        }
        // 말끝의 ㅋㅋ·ㄷㄷ는 세기에 맞는 길이로 친다(잔잔한 날 "ㅋㅋ", 난리 난 날 "ㅋㅋㅋㅋㅋㅋㅋ").
        return ChatVoice.After(line, Voice, level, random);
    }
}

/// <summary>영어: WSB·핀트윗 말투. "490%", "5.9x", "5-bagger". 소문자로만 치는 사람, 마침표를 안 찍는 사람, lol을 달고 사는 사람.</summary>
public sealed class ChatReactionEn : ChatReactionEngine
{
    public ChatReactionEn(ChatMarketLocale data) : base("en", data) { }
    protected override void Numbers(in ChatSituationRead read, IDictionary<string, string> slots)
    {
        string day = PercentTalk(read.dayPct, 1);
        if (day != null) { slots["dayTalk"] = day; }
        if (read.multiple >= 2m)
        {
            // 한국어·일본어와 달리 열 배 아래에서는 소수 한 자리까지 말한다("5.9x"). 올림하지 않는다.
            slots["multTalk"] = Data.numbers.multiple.Replace("{n}", MultipleNumber(read.multiple, true));
            // "n-bagger"는 채운 배수만 센다(5.9배는 아직 5-bagger다).
            if (!string.IsNullOrEmpty(Data.numbers.bagger)) { slots["bagger"] = Data.numbers.bagger.Replace("{n}", Math.Floor(read.multiple).ToString("0", Culture)); }
        }
    }
    public override string Dress(string line, ChatSituation kind, uint habit, Func<double> random)
    {
        var style = Data.style;
        bool lower = habit % 100u < style.lowercaseShare * 100, noPeriod = habit / 100u % 100u < style.dropPeriodShare * 100;
        if (lower) { line = LowerOutsideSlots(line); }
        if (noPeriod && line.Length > 1 && line[line.Length - 1] == '.' && line[line.Length - 2] != '.') { line = line.Substring(0, line.Length - 1); }
        // 말맛(ChatVoice.cs): 가끔 "bro"·"nah"로 말을 열고, lmao를 "lmaooo"로 늘인다. 얼마나 요란한지는 상황의 세기가 정한다.
        int level = ChatVoice.Level(Voice, kind);
        line = ChatVoice.Before(line, Voice, level, ChatVoice.Lean(kind), habit, random);
        bool tailed = habit / 10000u % 3u == 0u || EndsWithAny(line, "?!}") || line.EndsWith("lol", StringComparison.OrdinalIgnoreCase) || line.EndsWith("lmao", StringComparison.OrdinalIgnoreCase);
        if (!tailed && random() < style.tailChance)
        {
            string tail = Tail(kind, random);
            // 같은 말을 두 번 달지 않는다("ngl … ngl", "… fr fr"), 이미 말끝 버릇으로 끝난 말에도 달지 않는다("… lfg lol").
            if (tail != null && !HasWord(line, tail) && !EndsWithTail(line))
            { line = (line.Length > 0 && line[line.Length - 1] == '.' ? line.Substring(0, line.Length - 1) : line) + " " + tail; }
        }
        // 큰일에는 짧은 말을 가끔 대문자로 친다("NO SHOT").
        return ChatVoice.After(line, Voice, level, random);
    }
    private bool EndsWithTail(string line)
    {
        string trimmed = line.TrimEnd('.', ' ');
        foreach (string tail in Tails)
        {
            if (!string.IsNullOrEmpty(tail) && trimmed.EndsWith(tail, StringComparison.OrdinalIgnoreCase) && (trimmed.Length == tail.Length || !char.IsLetterOrDigit(trimmed[trimmed.Length - tail.Length - 1]))) { return true; }
        }
        return false;
    }
    private static string LowerOutsideSlots(string line)
    {
        var result = new StringBuilder(line.Length); bool slot = false;
        foreach (char c in line)
        {
            if (c == '{') { slot = true; } else if (c == '}') { slot = false; result.Append(c); continue; }
            result.Append(slot ? c : char.ToLowerInvariant(c));
        }
        return result.ToString();
    }
}

/// <summary>일본어: 5ch·株クラ 말투. "+490%", "6倍". 말끝의 ｗ·草. 전각 ｗ를 쓰는 사람과 반각 w를 쓰는 사람이 있다.</summary>
public sealed class ChatReactionJa : ChatReactionEngine
{
    public ChatReactionJa(ChatMarketLocale data) : base("ja", data) { }
    protected override void Numbers(in ChatSituationRead read, IDictionary<string, string> slots)
    {
        string day = PercentTalk(read.dayPct, 0);
        if (day != null) { slots["dayTalk"] = day; }
        if (read.multiple >= 2m) { slots["multTalk"] = Data.numbers.multiple.Replace("{n}", MultipleNumber(read.multiple, false)); }
    }
    public override string Dress(string line, ChatSituation kind, uint habit, Func<double> random)
    {
        bool halfWidth = habit % 2u == 1u;
        // 말맛(ChatVoice.cs): 가끔 "うわ、"로 말을 열고 말끝을 "ねえ"로 바꾼다. 얼마나 요란한지는 상황의 세기가 정한다.
        int level = ChatVoice.Level(Voice, kind);
        line = ChatVoice.Before(line, Voice, level, ChatVoice.Lean(kind), habit, random);
        if (habit / 2u % 3u == 0u && !EndsWithAny(line, "ｗw草?？…。」}") && random() < Data.style.tailChance)
        {
            string tail = Tail(kind, random);
            // 이미 ｗ·草가 든 말에 같은 것을 또 달지 않는다.
            if (tail != null && !RunAlready(line, tail) && line.IndexOf(tail, StringComparison.Ordinal) < 0) { line += tail; }
        }
        // 말끝의 ｗ는 세기에 맞는 개수로 친다(잔잔한 날 "ｗ", 난리 난 날 "ｗｗｗｗｗ").
        line = ChatVoice.After(line, Voice, level, random);
        // 반각 w를 쓰는 사람은 파일에 전각으로 적힌 말끝도 반각으로 친다(개수는 그대로).
        if (halfWidth && line.Length > 0 && line[line.Length - 1] == 'ｗ')
        {
            string kept = line.TrimEnd('ｗ');
            line = kept + new string('w', line.Length - kept.Length);
        }
        return line;
    }
}

/// <summary>언어별 시세 반응 파일(Chat/chat_market.ko/en/ja)을 읽어 엔진을 만든다. 읽지 못한 언어는 엔진이 없다.</summary>
public static class ChatReactionAssets
{
    private static Dictionary<string, ChatReactionEngine> engines;
    /// <summary>언어 코드로 파일 내용을 돌려주는 함수. 비워 두면 Resources에서 읽는다(에디터 점검 도구는 파일에서 바로 읽는다).</summary>
    public static Func<string, string> Reader;
    /// <summary>읽다가 생긴 문제(언어별). 점검 도구가 보고서에 적는다.</summary>
    public static readonly List<string> Problems = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { engines = null; Reader = null; Problems.Clear(); }

    public static ChatReactionEngine Current => Get(GameLocale.Code);
    /// <summary>그 언어의 말맛 규칙(ChatVoice.cs). 그 언어의 파일이 없거나 규칙이 적혀 있지 않으면 null.</summary>
    public static ChatVoiceRules Voice(string code) => Get(code)?.Voice;
    public static ChatReactionEngine Get(string code)
    {
        if (engines == null) { Reload(); }
        return engines.TryGetValue(code ?? "", out var engine) ? engine : null;
    }
    public static void Reload()
    {
        var loaded = new Dictionary<string, ChatReactionEngine>(StringComparer.Ordinal); Problems.Clear();
        foreach (string code in ChatHypeAssets.Languages)
        {
            try
            {
                string json = Reader != null ? Reader(code) : Resources.Load<TextAsset>("Chat/chat_market." + code)?.text;
                if (string.IsNullOrEmpty(json)) { Problems.Add(code + ": missing chat_market." + code); continue; }
                var engine = Build(code, JsonUtility.FromJson<ChatMarketLocale>(json));
                if (engine != null) { loaded[code] = engine; }
            }
            catch (ArgumentException ex) { Problems.Add(code + ": " + ex.Message); Debug.LogWarning("시세 반응 대사를 읽지 못했습니다(" + code + "). 이 언어는 예전 대사로 말합니다: " + ex.Message); }
        }
        engines = loaded;
    }
    private static ChatReactionEngine Build(string code, ChatMarketLocale data)
    {
        if (data == null || data.schemaVersion != 1 || data.groups == null || data.groups.Length == 0) { throw new ArgumentException("Invalid chat_market schema"); }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in data.groups)
        {
            if (group == null || string.IsNullOrEmpty(group.key) || group.texts == null || group.texts.Length == 0 || !seen.Add(group.key)) { throw new ArgumentException("Invalid/duplicate chat_market group: " + group?.key); }
            foreach (string text in group.texts) { if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("Empty chat_market text: " + group.key); } }
        }
        // 상황마다 세 처지의 대사가 모두 있어야 한다. 하나라도 빠지면 그 상황에서 그 처지의 사람만 예전 대사로 말하게 되어 뒤섞인다.
        foreach (ChatSituation kind in Enum.GetValues(typeof(ChatSituation)))
        {
            if (kind == ChatSituation.Quiet) { continue; }
            foreach (string stance in new[] { "gain", "loss", "watch" }) { if (!seen.Contains(kind + "." + stance)) { throw new ArgumentException("Missing chat_market lines: " + kind + "." + stance); } }
        }
        return code == "ko" ? new ChatReactionKo(data) : code == "ja" ? new ChatReactionJa(data) : (ChatReactionEngine)new ChatReactionEn(data);
    }
}
