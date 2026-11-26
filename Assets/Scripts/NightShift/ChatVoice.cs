using System;
using System.Collections.Generic;
using System.Text;

// 말맛 입히기. 같은 대사 묶음에서 나온 말이 매번 조금씩 다른 꼴로 뜨게 한다.
//
// 대사 파일에는 "와 쩐다 ㄷㄷ" 한 줄이 있을 뿐이다. 사람은 그 말을 그때그때 다르게 친다:
//   큰일에는 ㄷ을 아홉 개 치고, 잔잔한 날에는 두 개만 친다. 가끔 "와"·"아니"로 말을 열고, "진짜"·"ㄹㅇ"을 끼운다.
//   영어권은 lmao를 "lmaooo"로 늘이고 큰일에는 대문자로 소리친다. 일본어권은 ｗ를 여러 개 치고 말끝을 "ねえ"로 바꾼다.
// 여기서는 그 버릇을 규칙으로 적는다. 규칙과 확률은 언어마다 따로 쓴 파일(Chat/chat_market.ko / .en / .ja 의 "voice")에서 온다. 서로의 번역이 아니다.
//
// 세기(0 잔잔함 · 1 눈에 띔 · 2 큰일 · 3 난리)는 말하는 사람이 아니라 상황이 정한다:
//   시세 반응은 상황(ChatSituation)에서, 인증 카드 반응은 금액 단(ChatProofAmountTier)에서, 급등락 군중은 폭주의 단계에서 읽는다.
//   잔잔한 날의 말에는 긴 ㅋ·ㄷ 줄이 붙지 않는다(파일에 길게 적혀 있어도 그 세기의 윗선까지만 친다).
// 버릇(habit)은 봇마다 정해진 값이다: 셋 중 하나는 말머리를 달지 않는다.
// 난수는 부르는 쪽이 준 것만 쓴다(씨앗이 같으면 같은 말이 나온다). 게임 상태는 건드리지 않으므로 작업 스레드에서도 부를 수 있다.
//
// 짧은 맞장구("ㄹㅇ", "ㅇㅈ", "fr", "それな")도 여기서 고른다(Back). 얼마나 자주 쓸지는 부르는 쪽이 가린다(InvestorSociety.BackDue):
// 라운지에서만, 몇 초에 한 번만. 새벽 관찰실에는 두지 않는다(ChatDawnRoom.cs: 그 방은 근거를 대고 말한다).

public enum ChatVoiceLean { Up, Down, Calm }

/// <summary>한 언어의 말맛 규칙. 세기별 값은 [잔잔함, 눈에 띔, 큰일, 난리]의 네 칸이다.</summary>
[Serializable] public sealed class ChatVoiceRules
{
    /// <summary>상황마다의 세기(0~3). 없는 상황은 ChatVoice의 기본값을 쓴다.</summary>
    public SocialWeight[] levels;

    // ── 늘여 치는 글자(ㅋㅋㅋ, ㄷㄷㄷ, ｗｗｗ) ──
    /// <summary>늘여 치는 글자들. 한국어 "ㅋㄷㅠㅎ", 일본어 "ｗw". 비우면 이 규칙을 쓰지 않는다.</summary>
    public string runChars = "";
    /// <summary>말끝에서 이만큼 이어져 있으면 길이를 다시 정한다(한국어 2: "ㅋㅋ"부터, 일본어 1: "ｗ" 하나도).</summary>
    public int runFrom = 2;
    /// <summary>세기별 길이의 아랫선과 윗선. 윗선을 넘는 줄은 문장 어디에 있든 윗선까지 줄인다.</summary>
    public int[] runMin, runMax;
    /// <summary>말끝의 줄 길이를 다시 정할 확률. 나머지는 파일에 적힌 길이 그대로(윗선은 지킨다).</summary>
    public double runVaryChance = .6;

    // ── 말머리("와", "아니", "bro", "うわ") ──
    public string[] openersUp, openersDown, openersCalm;
    /// <summary>큰일(세기 2 이상)에만 더해지는 말머리("헐", "와").</summary>
    public string[] openersStrong;
    /// <summary>말머리와 문장 사이에 넣는 것. 한국어·영어는 빈칸, 일본어는 "、".</summary>
    public string openerJoin = " ";
    /// <summary>세기별로 말머리가 붙을 확률.</summary>
    public double[] openerChance;
    /// <summary>이런 말로 시작하는 문장에는 말머리도 앞에 끼우는 말도 붙이지 않는다(이미 감탄사로 시작한다).</summary>
    public string[] openerBlock;
    /// <summary>말머리를 붙일 때 문장의 첫 글자를 소문자로 바꾸는가(영어).</summary>
    public bool lowerAfterOpener;
    /// <summary>묻는 말에는 말머리를 붙이지 않는가(일본어: "うお、いつ売るん？"은 어색하다).</summary>
    public bool openerSkipsQuestions;
    /// <summary>이보다 짧은 말에는 말머리를 붙이지 않는다(영어: "man cooked"는 어색하다. 한국어의 "와 ㄷㄷㄷ"은 괜찮으므로 0).</summary>
    public int openerMinLength;

    // ── 끼우는 말("진짜", "ㄹㅇ", "솔직히", "ngl", "正直") ──
    public string[] softenersLead, softenersTrail;
    public string softenerLeadJoin = " ", softenerTrailJoin = " ";
    /// <summary>뒤에 끼우는 말은 이런 말끝 뒤에만 붙인다(비우면 가리지 않는다). 단정·짐작하는 말끝을 적는다.</summary>
    public string[] softenersTrailOnlyAfter;
    /// <summary>끼우는 말이 붙을 확률(말머리가 안 붙었을 때만 굴린다). 묻는 말에는 붙이지 않는다.</summary>
    public double softenerChance;

    // ── 힘주는 말머리(한국어 "개쩐다", "개비싸네", "개높네") ──
    /// <summary>
    /// 낱말 앞에 바로 붙여 힘을 주는 말(한국어의 "개"). 비어 있으면 쓰지 않는다(영어·일본어).
    /// boostStems: 이 말로 시작하는 낱말에만 붙인다(쩐다, 비싸, 높네, 부럽, 올랐, 노잼, 이득 …). 아무 낱말에나 붙이면 말이 안 된다.
    /// boostChance: 세기별 확률. 잔잔한 날에는 드물고, 난리일수록 잦다.
    /// boostBlock: 이 말로 시작하는 낱말에는 붙이지 않는다(이미 힘준 말, 붙이면 뜻이 달라지는 말).
    /// </summary>
    public string boostPrefix = "";
    public string[] boostStems, boostBlock, boostAfterBlock;
    public double[] boostChance;

    // ── 글자 늘이기(영어 "lmaooo", "nahhh") ──
    /// <summary>"낱말>늘일 글자". 그 낱말이 문장에 따로 서 있을 때만 늘인다("lmao>o", "nah>h").</summary>
    public string[] stretch;
    /// <summary>세기별 확률과, 덧붙는 글자 수의 윗선.</summary>
    public double[] stretchChance; public int[] stretchMax;
    /// <summary>세기별로 문장 전체를 대문자로 칠 확률(짧은 말만: capsMaxLength 글자 이하, 빈칸이 없는 말).</summary>
    public double[] capsChance; public int capsMaxLength = 24;

    // ── 말끝 바꾸기(일본어 "ない" → "ねえ") ──
    /// <summary>"원래 말끝|바꿀 말끝|바꿀 말끝". 긴 것을 먼저 적는다.</summary>
    public string[] endings;
    public double endingChance;

    // ── 맞장구 ──
    public string[] backAgree, backLaugh, backAwe, backSad;
    /// <summary>남의 말에 한두 마디 맞장구로 답할 확률, 맞장구 사이에 두는 틈(초), 폭주 한 번에 넣는 맞장구의 윗선.</summary>
    public double backChance; public double backGapSec = 10; public int backMaxPerRun = 2;
}

public static class ChatVoice
{
    public const int Mild = 0, Warm = 1, Strong = 2, Extreme = 3;
    private static readonly string[] None = Array.Empty<string>();

    private static int Clamp(int level) => level < Mild ? Mild : level > Extreme ? Extreme : level;
    private static double At(double[] values, int level) => values == null || values.Length == 0 ? 0 : values[Math.Min(level, values.Length - 1)];
    private static int At(int[] values, int level, int fallback) => values == null || values.Length == 0 ? fallback : values[Math.Min(level, values.Length - 1)];
    private static string Pick(string[] pool, Func<double> random) => pool == null || pool.Length == 0 ? null : pool[(int)(random() * pool.Length) % pool.Length];

    // ── 세기 ──
    /// <summary>이 상황의 세기. 파일에 적힌 값이 먼저고, 없으면 기본값: 잔잔한 날 0, 눌림·반등 1, 돌파·급등·급락 2, 괴물 급등·투매 3.</summary>
    public static int Level(ChatVoiceRules rules, ChatSituation kind)
    {
        if (rules?.levels != null)
        {
            string name = kind.ToString();
            foreach (var row in rules.levels) { if (row != null && row.key == name) { return Clamp((int)row.value); } }
        }
        switch (kind)
        {
            case ChatSituation.MonsterRally: case ChatSituation.Capitulation: return Extreme;
            case ChatSituation.Breakout: case ChatSituation.Surge: case ChatSituation.Crash: return Strong;
            case ChatSituation.Pullback: case ChatSituation.RoundTrip: case ChatSituation.SlowBleed: case ChatSituation.DeadCatBounce: case ChatSituation.Recovery: return Warm;
            default: return Mild;
        }
    }
    /// <summary>오르는 얘기인가, 내리는 얘기인가, 덤덤한 얘기인가. 말끝 버릇(ChatReactionEngine.Tail)과 같은 갈래다.</summary>
    public static ChatVoiceLean Lean(ChatSituation kind)
        => ChatSituationRules.Rising(kind) || kind == ChatSituation.Recovery ? ChatVoiceLean.Up :
            ChatSituationRules.Falling(kind) || kind == ChatSituation.Pullback || kind == ChatSituation.DeadCatBounce ? ChatVoiceLean.Down : ChatVoiceLean.Calm;
    /// <summary>
    /// 인증 카드 반응의 세기는 금액 단에서 온다(보는 사람의 재산에 견준 단): 푼돈·밥값 0, 괜찮은 하루 1, 월급 2, 차 한 대부터 3.
    /// 판돈에 놀라는 기분(StakeAwe·StakeOuch)은 손익금이 작아도 2부터다.
    /// </summary>
    public static int ProofLevel(in ChatProofRead read)
    {
        int level = read.tier >= ChatProofAmountTier.Car ? Extreme : read.tier == ChatProofAmountTier.Salary ? Strong : read.tier == ChatProofAmountTier.GoodDay ? Warm : Mild;
        if ((read.mood == ChatProofMood.StakeAwe || read.mood == ChatProofMood.StakeOuch) && level < Strong) { level = Strong; }
        return level;
    }
    public static ChatVoiceLean ProofLean(in ChatProofRead read)
        => read.mood == ChatProofMood.TeaseSmall || read.mood == ChatProofMood.ShrugLoss ? ChatVoiceLean.Calm : read.gain ? ChatVoiceLean.Up : ChatVoiceLean.Down;

    // ── 입히기 ──
    /// <summary>말머리·끼우는 말·글자 늘이기·말끝 바꾸기. 말끝 버릇(ㅋㅋ, lol, ｗ)을 붙이기 전에 부른다. 빈칸({…})과 멘션(@)은 건드리지 않는다.</summary>
    public static string Before(string line, ChatVoiceRules rules, int level, ChatVoiceLean lean, uint habit, Func<double> random)
    {
        if (rules == null || string.IsNullOrEmpty(line) || line[0] == '@') { return line; }
        level = Clamp(level);
        // 버릇: 셋 중 하나는 말머리도 끼우는 말도 쓰지 않는다.
        bool leads = habit / 3u % 3u != 0u;
        if (leads && random() < At(rules.openerChance, level))
        {
            string opener = Pick(Openers(rules, level, lean), random);
            if (opener != null && line.Length >= rules.openerMinLength && !StartsLikeOpener(line, rules) && !(rules.openerSkipsQuestions && Asks(line, rules))) { line = opener + rules.openerJoin + LowerFirst(line, rules.lowerAfterOpener); }
        }
        else if (leads && rules.softenerChance > 0 && random() < rules.softenerChance && !Asks(line, rules)) { line = Soften(line, rules, random); }
        line = Boost(line, rules, level, habit, random);
        line = Stretch(line, rules, level, random);
        return Ending(line, rules, random);
    }
    /// <summary>늘여 치는 글자의 길이를 세기에 맞추고, 큰일에는 가끔 대문자로 친다. 말끝 버릇을 붙인 뒤에 부른다.</summary>
    /// <remarks>shout가 false면 대문자로 치지 않는다(남의 손실을 두고 하는 말은 소리치지 않는다).</remarks>
    public static string After(string line, ChatVoiceRules rules, int level, Func<double> random, bool shout = true)
    {
        if (rules == null || string.IsNullOrEmpty(line)) { return line; }
        level = Clamp(level);
        line = Runs(line, rules, level, random);
        return shout ? Caps(line, rules, level, random) : line;
    }
    /// <summary>말끝 버릇을 따로 붙이지 않는 말(인증 반응, 급등락 군중, 차트 수다, 잔움직임)에 한 번에 입힌다.</summary>
    public static string Apply(string line, ChatVoiceRules rules, int level, ChatVoiceLean lean, uint habit, Func<double> random, bool shout = true)
        => After(Before(line, rules, level, lean, habit, random), rules, level, random, shout);

    /// <summary>
    /// 짧은 맞장구 한마디. mood: "agree"(ㄹㅇ·ㅇㅈ) · "laugh"(ㅋㅋㅋㅋ) · "awe"(ㄷㄷ·와…) · "sad"(ㅠㅠ·하…). 없으면 null.
    /// recent: 방금 나온 맞장구인가(그런 말은 피한다).
    /// </summary>
    public static string Back(ChatVoiceRules rules, string mood, int level, Func<double> random, Func<string, bool> recent)
    {
        if (rules == null) { return null; }
        var pool = mood == "laugh" ? rules.backLaugh : mood == "awe" ? rules.backAwe : mood == "sad" ? rules.backSad : rules.backAgree;
        if (pool == null || pool.Length == 0) { return null; }
        int start = (int)(random() * pool.Length) % pool.Length;
        string chosen = pool[start];
        for (int i = 0; i < pool.Length && recent != null && recent(chosen); i++) { chosen = pool[(start + i + 1) % pool.Length]; }
        return After(Stretch(chosen, rules, Clamp(level), random), rules, level, random);
    }
    /// <summary>오르는 얘기·내리는 얘기에 어울리는 맞장구의 갈래를 고른다.</summary>
    public static string BackMood(ChatVoiceLean lean, double roll)
        => lean == ChatVoiceLean.Up ? (roll < .4 ? "awe" : roll < .7 ? "agree" : "laugh") : lean == ChatVoiceLean.Down ? (roll < .5 ? "sad" : "agree") : (roll < .7 ? "agree" : "laugh");

    private static string[] Openers(ChatVoiceRules rules, int level, ChatVoiceLean lean)
    {
        var basic = (lean == ChatVoiceLean.Up ? rules.openersUp : lean == ChatVoiceLean.Down ? rules.openersDown : rules.openersCalm) ?? None;
        if (level < Strong || rules.openersStrong == null || rules.openersStrong.Length == 0) { return basic; }
        var all = new string[basic.Length + rules.openersStrong.Length];
        basic.CopyTo(all, 0); rules.openersStrong.CopyTo(all, basic.Length);
        return all;
    }
    private static bool StartsWith(string line, string word, bool spaced)
    {
        if (string.IsNullOrEmpty(word) || !line.StartsWith(word, StringComparison.OrdinalIgnoreCase)) { return false; }
        // 띄어 쓰는 언어에서는 낱말이 거기서 끝나야 한다("아 팔 걸"은 막고 "아침에 살 걸"은 막지 않는다).
        return !spaced || line.Length == word.Length || !char.IsLetterOrDigit(line[word.Length]);
    }
    private static bool StartsLikeOpener(string line, ChatVoiceRules rules)
    {
        bool spaced = rules.openerJoin == " ";
        foreach (var list in new[] { rules.openerBlock, rules.openersUp, rules.openersDown, rules.openersCalm, rules.openersStrong, rules.softenersLead })
        {
            if (list == null) { continue; }
            foreach (string word in list) { if (StartsWith(line, word, spaced)) { return true; } }
        }
        return false;
    }
    private static string LowerFirst(string line, bool lower)
        => lower && line.Length > 1 && char.IsUpper(line[0]) && char.IsLower(line[1]) ? char.ToLowerInvariant(line[0]) + line.Substring(1) : line;
    private static bool Asks(string line, ChatVoiceRules rules)
    {
        int end = line.Length;
        while (end > 0 && (line[end - 1] == ' ' || (rules.runChars ?? "").IndexOf(line[end - 1]) >= 0 || line[end - 1] == '草')) { end--; }
        return end > 0 && (line[end - 1] == '?' || line[end - 1] == '？');
    }
    private static string Soften(string line, ChatVoiceRules rules, Func<double> random)
    {
        bool hasLead = rules.softenersLead != null && rules.softenersLead.Length > 0, hasTrail = rules.softenersTrail != null && rules.softenersTrail.Length > 0;
        if (hasLead && (!hasTrail || random() < .5))
        {
            string word = Pick(rules.softenersLead, random);
            return line.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 || StartsLikeOpener(line, rules) ? line : word + rules.softenerLeadJoin + LowerFirst(line, rules.lowerAfterOpener);
        }
        if (hasTrail)
        {
            string word = Pick(rules.softenersTrail, random); char last = line[line.Length - 1];
            // 글자로 끝난 말에만 붙인다(ㅋㅋ·문장부호·빈칸으로 끝난 말에는 붙이지 않는다).
            if (char.IsLetter(last) && (rules.runChars ?? "").IndexOf(last) < 0 && line.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0 && TrailFits(line, rules)) { return line + rules.softenerTrailJoin + word; }
        }
        return line;
    }

    /// <summary>뒤에 끼우는 말이 어울리는 말끝인가. 단정하거나 짐작하는 말에만 붙는다("바닥이었네 ㄹㅇ", "底だろ、知らんけど"): 시키는 말·외마디에는 붙이지 않는다.</summary>
    private static bool TrailFits(string line, ChatVoiceRules rules)
    {
        if (rules.softenersTrailOnlyAfter == null || rules.softenersTrailOnlyAfter.Length == 0) { return true; }
        foreach (string ending in rules.softenersTrailOnlyAfter) { if (!string.IsNullOrEmpty(ending) && line.EndsWith(ending, StringComparison.Ordinal)) { return true; } }
        return false;
    }
    /// <summary>
    /// 힘주는 말머리를 낱말 하나에 붙인다: "비싸네" → "개비싸네", "쩐다" → "개쩐다", "올랐네" → "개올랐네".
    /// 한 문장에 한 번만, 정해 둔 말로 시작하는 낱말에만 붙인다(뒤쪽 낱말부터 찾는다: 한국어는 힘줄 말이 대개 끝에 온다).
    /// 사람마다 버릇이 다르다: 넷 중 하나는 이런 말을 아예 안 쓰고, 넷 중 하나는 입에 달고 산다(확률 1.8배).
    /// 빈칸({…})이나 멘션(@), 숫자가 든 낱말은 건드리지 않는다.
    /// </summary>
    private static string Boost(string line, ChatVoiceRules rules, int level, uint habit, Func<double> random)
    {
        if (string.IsNullOrEmpty(rules.boostPrefix) || rules.boostStems == null || rules.boostStems.Length == 0) { return line; }
        uint taste = habit / 7u % 4u;
        if (taste == 0u) { return line; }
        double chance = At(rules.boostChance, level) * (taste == 3u ? 1.8 : 1.0);
        if (chance <= 0 || random() >= chance) { return line; }
        string[] words = line.Split(' ');
        for (int i = words.Length - 1; i >= 0; i--)
        {
            string word = words[i];
            if (word.Length < 2 || word[0] == '{' || word[0] == '@' || word.StartsWith(rules.boostPrefix, StringComparison.Ordinal)) { continue; }
            bool plain = true;
            foreach (char c in word) { if (char.IsDigit(c) || c == '{' || c == '}') { plain = false; break; } }
            if (!plain || Begins(word, rules.boostBlock) || !Begins(word, rules.boostStems)) { continue; }
            // 바로 앞의 낱말이 이미 힘주는 말이면 그대로 둔다("개 비싸네"처럼 띄어 쓴 경우).
            if (i > 0 && words[i - 1] == rules.boostPrefix) { continue; }
            // "좀 빠지네"·"많이 올랐다"처럼 앞말이 정도를 정해 둔 낱말에는 붙이지 않는다. 앞말이 붙일 수 있는 말이면 그쪽에 붙는다("개많이 올랐다").
            if (i > 0 && rules.boostAfterBlock != null && Array.IndexOf(rules.boostAfterBlock, words[i - 1]) >= 0) { continue; }
            // 숫자 바로 뒤("3퍼 올랐네")는 사실을 말하는 자리라 힘을 주지 않는다.
            if (i > 0 && words[i - 1].Length > 0 && (words[i - 1][0] == '{' || char.IsDigit(words[i - 1][0]))) { continue; }
            words[i] = rules.boostPrefix + word;
            return string.Join(" ", words);
        }
        return line;
    }
    private static bool Begins(string word, string[] starts)
    {
        if (starts == null) { return false; }
        foreach (string start in starts) { if (!string.IsNullOrEmpty(start) && word.StartsWith(start, StringComparison.Ordinal)) { return true; } }
        return false;
    }
    private static bool WordChar(char c) => char.IsLetterOrDigit(c) || c == '\'' || c == '’';
    /// <summary>늘일 수 있는 낱말 하나를 골라 글자를 덧붙인다("lmao" → "lmaooo"). 빈칸 안은 건드리지 않는다.</summary>
    private static string Stretch(string line, ChatVoiceRules rules, int level, Func<double> random)
    {
        if (rules.stretch == null || rules.stretch.Length == 0) { return line; }
        double chance = At(rules.stretchChance, level);
        if (chance <= 0 || random() >= chance) { return line; }
        int foundAt = -1, foundLength = 0, seen = 0; char letter = '\0'; bool slot = false;
        // 늘여 치는 것은 문장을 여는 말과 닫는 말뿐이다("whyyy won't it bounce", "look at it go lmaooo"). 문장 한가운데의 낱말은 늘이지 않는다.
        int firstWord = 0; while (firstWord < line.Length && !WordChar(line[firstWord]) && line[firstWord] != '{') { firstWord++; }
        int lastEnd = line.Length; while (lastEnd > 0 && !WordChar(line[lastEnd - 1]) && line[lastEnd - 1] != '}') { lastEnd--; }
        for (int i = 0; i < line.Length;)
        {
            if (line[i] == '{') { slot = true; } else if (line[i] == '}') { slot = false; }
            if (slot || !WordChar(line[i])) { i++; continue; }
            int end = i; while (end < line.Length && WordChar(line[end])) { end++; }
            if (i != firstWord && end != lastEnd) { i = end; continue; }
            foreach (string rule in rules.stretch)
            {
                int mark = rule.IndexOf('>');
                if (mark <= 0 || mark != end - i || mark + 1 >= rule.Length || string.Compare(line, i, rule, 0, mark, StringComparison.OrdinalIgnoreCase) != 0) { continue; }
                seen++; if ((int)(random() * seen) == 0) { foundAt = i; foundLength = mark; letter = rule[mark + 1]; }
                break;
            }
            i = end;
        }
        if (foundAt < 0) { return line; }
        string word = line.Substring(foundAt, foundLength);
        int at = word.LastIndexOf(letter.ToString(), StringComparison.OrdinalIgnoreCase);
        if (at < 0) { return line; }
        // 잔잔한 날에는 한 글자만 더 친다("nahh"). 그 위로는 두 글자부터다: 한 글자만 늘이면 오타처럼 보인다("lmaoo"보다 "lmaooo").
        int most = Math.Max(1, At(rules.stretchMax, level, 2));
        int extra = level == Mild || most < 2 ? 1 : 2 + (int)(random() * (most - 1));
        return line.Substring(0, foundAt + at + 1) + new string(word[at], extra) + line.Substring(foundAt + at + 1);
    }
    private static string Ending(string line, ChatVoiceRules rules, Func<double> random)
    {
        if (rules.endings == null || rules.endings.Length == 0 || rules.endingChance <= 0 || random() >= rules.endingChance) { return line; }
        foreach (string rule in rules.endings)
        {
            string[] parts = rule.Split('|');
            if (parts.Length < 2 || parts[0].Length == 0 || !line.EndsWith(parts[0], StringComparison.Ordinal)) { continue; }
            return line.Substring(0, line.Length - parts[0].Length) + parts[1 + (int)(random() * (parts.Length - 1)) % (parts.Length - 1)];
        }
        return line;
    }
    /// <summary>늘여 친 글자 줄: 말끝의 줄은 세기에 맞는 길이로 다시 정하고(가끔), 어디에 있든 그 세기의 윗선을 넘지 않게 한다.</summary>
    private static string Runs(string line, ChatVoiceRules rules, int level, Func<double> random)
    {
        if (string.IsNullOrEmpty(rules.runChars)) { return line; }
        int max = Math.Max(1, At(rules.runMax, level, 4)), min = Math.Max(1, Math.Min(max, At(rules.runMin, level, 2)));
        var result = new StringBuilder(line.Length + max);
        for (int i = 0; i < line.Length;)
        {
            char c = line[i];
            if (c == '{') { int close = line.IndexOf('}', i); if (close < 0) { close = line.Length - 1; } result.Append(line, i, close - i + 1); i = close + 1; continue; }
            if (rules.runChars.IndexOf(c) < 0) { result.Append(c); i++; continue; }
            int end = i; while (end < line.Length && line[end] == c) { end++; }
            int length = end - i;
            // 낱말 속의 한 글자(영문 낱말 끝의 w 같은 것)는 웃음이 아니다.
            bool laugh = length >= 2 || i == 0 || line[i - 1] > 0x7f || line[i - 1] == ' ';
            if (end == line.Length && laugh && length >= Math.Max(1, rules.runFrom) && random() < rules.runVaryChance) { length = min + (int)(random() * (max - min + 1)); }
            if (laugh && length > max) { length = max; }
            result.Append(c, length); i = end;
        }
        return result.ToString();
    }
    private static string Caps(string line, ChatVoiceRules rules, int level, Func<double> random)
    {
        double chance = At(rules.capsChance, level);
        if (chance <= 0 || line.Length > rules.capsMaxLength || line.IndexOf('{') >= 0 || random() >= chance) { return line; }
        return line.ToUpperInvariant();
    }

    // ── 점검(에디터 점검 도구가 쓴다) ──
    /// <summary>늘여 친 글자가 가장 길게 이어진 길이.</summary>
    public static int LongestRun(string line, ChatVoiceRules rules)
    {
        int longest = 0;
        if (rules == null || string.IsNullOrEmpty(rules.runChars) || string.IsNullOrEmpty(line)) { return 0; }
        for (int i = 0; i < line.Length;)
        {
            if (rules.runChars.IndexOf(line[i]) < 0) { i++; continue; }
            int end = i; while (end < line.Length && line[end] == line[i]) { end++; }
            longest = Math.Max(longest, end - i); i = end;
        }
        return longest;
    }
    private static bool PureRun(string token, ChatVoiceRules rules)
    {
        if (token.Length == 0 || string.IsNullOrEmpty(rules.runChars)) { return false; }
        foreach (char c in token) { if (rules.runChars.IndexOf(c) < 0) { return false; } }
        return true;
    }
    /// <summary>
    /// 화면에 뜰 한 줄(빈칸까지 채운 글)이 말맛 규칙을 지켰는가. 어긋난 점을 한 줄로 돌려준다. 괜찮으면 null.
    ///   빈 줄 · 채워지지 않은 빈칸 · 겹친 말끝("ㄷㄷ ㄷㄷ", "lol lol", "草草") · 그 세기의 윗선을 넘는 ㅋ·ㄷ·ｗ 줄 · 그 세기에 맞지 않게 늘인 글자("nahhhhhh")
    /// tails는 그 언어의 말끝 버릇들이다(ChatMarketStyle의 tailsUp·tailsDown·tailsCalm).
    /// </summary>
    public static string Audit(string said, ChatVoiceRules rules, int level, IEnumerable<string> tails)
    {
        if (string.IsNullOrWhiteSpace(said)) { return "빈 줄이다"; }
        if (said.IndexOf('{') >= 0 || said.IndexOf('}') >= 0) { return "빈칸이 채워지지 않았다"; }
        if (rules == null) { return null; }
        level = Clamp(level);
        var known = new List<string>();
        if (tails != null) { foreach (string tail in tails) { if (!string.IsNullOrEmpty(tail)) { known.Add(tail); } } }
        string[] tokens = said.TrimEnd().Split(' ');
        if (tokens.Length >= 2)
        {
            string last = tokens[tokens.Length - 1], before = tokens[tokens.Length - 2];
            // 같은 말을 되풀이하는 것("간다 간다", "drip drip")은 말버릇이다. 말끝 버릇이 두 번 붙은 것만 잡는다.
            bool tailWord = known.Exists(tail => string.Equals(tail, last, StringComparison.OrdinalIgnoreCase)) ||
                rules.softenersTrail != null && Array.Exists(rules.softenersTrail, word => string.Equals(word, last, StringComparison.OrdinalIgnoreCase));
            if (tailWord && string.Equals(last, before, StringComparison.OrdinalIgnoreCase)) { return "말끝이 겹쳤다(" + before + " " + last + ")"; }
            if (PureRun(last, rules) && PureRun(before, rules)) { return "말끝이 겹쳤다(" + before + " " + last + ")"; }
        }
        if (known.Count > 0)
        {
            foreach (string first in known)
            {
                foreach (string second in known)
                {
                    // 같은 글자를 이어 친 것("ｗ"+"ｗｗ")은 한 줄이다. 길이는 아래에서 따로 본다.
                    if (PureRun(first + second, rules) && first[0] == second[0]) { continue; }
                    if (said.EndsWith(first + second, StringComparison.OrdinalIgnoreCase) && (first.Length > 1 || second.Length > 1 || first == second) && !PureRun(first, rules) && !PureRun(second, rules) ||
                        said.EndsWith(" " + first + " " + second, StringComparison.OrdinalIgnoreCase))
                    { return "말끝이 겹쳤다(" + first + " + " + second + ")"; }
                }
            }
        }
        int cap = At(rules.runMax, level, int.MaxValue), longest = LongestRun(said, rules);
        if (longest > cap) { return "세기 " + level + "에 " + longest + "글자 줄이 붙었다(윗선 " + cap + ")"; }
        if (rules.stretch != null && rules.stretch.Length > 0)
        {
            int limit = 2 + Math.Max(1, At(rules.stretchMax, level, 2)), streak = 1;
            for (int i = 1; i < said.Length; i++)
            {
                streak = char.IsLetter(said[i]) && char.ToLowerInvariant(said[i]) == char.ToLowerInvariant(said[i - 1]) ? streak + 1 : 1;
                if (streak > limit) { return "세기 " + level + "에 글자를 " + streak + "번 늘였다(윗선 " + limit + ")"; }
            }
        }
        return null;
    }
}
