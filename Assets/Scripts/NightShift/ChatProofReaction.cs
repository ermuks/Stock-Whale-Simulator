using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 인증 카드(수익 인증·보유 인증)를 본 사람의 반응.
//
// 예전에는 수익률만 봤다. 그래서 +1%로 500돈을 번 카드와 +1%로 5,000만돈을 번 카드에 같은 말("오 +1% ㄷㄷ")이 달렸다.
// 여기서는 카드를 두 축으로 읽는다:
//   금액 단(ChatProofAmountTier)  푼돈 < 1만 · 밥값 < 100만 · 괜찮은 하루 < 1,000만 · 월급 < 1억 · 차 한 대 < 10억 · 인생이 바뀌는 돈
//   수익률 단(ChatProofPercentTier) 얇다 < 3% · 쏠쏠 < 15% · 크다 < 50% · 엄청나다
// 그리고 보는 사람의 재산에 견준다: 그 돈이 내 재산의 0.1%도 안 되면 한 단 덜 놀라고, 내 재산 전체보다 많으면 한 단 더 놀란다(푼돈은 누가 봐도 푼돈이다).
// 두 축을 겹쳐 반응의 기분(ChatProofMood)을 정한다:
//   금액이 푼돈                       → 가볍게 놀린다("겨우 그거 가지고 뭐 할라고 ㅋㅋ")
//   수익률은 큰데 금액이 작다         → "퍼센트는 좋은데 시드가…"
//   수익률은 얇은데 금액이 크다       → 판돈에 놀란다("1%에 5천만이면 시드가 얼마냐")
//   둘 다 크다                        → 부러워하고, 놀라고, 종목을 묻는다
//   손실은 거울처럼: 작은 손실은 어깨를 으쓱하거나 놀리고, 큰 손실에는 놀라거나 위로하거나 쓴웃음을 짓는다.
//   보유 인증은 손익금만이 아니라 들고 있는 크기(판돈)도 본다: 10억어치를 들고 있으면 수익이 얼마든 그 크기에 먼저 놀란다.
// 놀리는 말은 장난이어야 한다. 사람을 깎아내리는 말은 쓰지 않는다. 속상해하는 사람에게는 놀림(.edge 묶음)을 빼고 고른다.
// 문장은 언어마다 따로 쓴 파일(Chat/chat_proof.ko / .en / .ja)에 있다. 번역이 아니다.

public enum ChatProofAmountTier { Pocket, Lunch, GoodDay, Salary, Car, Life }
public enum ChatProofPercentTier { Thin, Solid, Big, Huge }
public enum ChatProofMood
{
    /// <summary>푼돈 수익. 가볍게 놀린다.</summary>
    TeaseSmall,
    /// <summary>밥값~괜찮은 하루. 덤덤하게 축하한다.</summary>
    Modest,
    /// <summary>수익률은 큰데 금액이 작다.</summary>
    PctNoSeed,
    /// <summary>수익률은 얇은데 금액(또는 들고 있는 크기)이 크다. 판돈에 놀란다.</summary>
    StakeAwe,
    /// <summary>월급만큼 벌었다. 부러워하고 종목을 묻는다.</summary>
    Envy,
    /// <summary>금액도 수익률도 크다. 충격.</summary>
    Shock,
    /// <summary>푼돈 손실. 어깨를 으쓱하거나 놀린다.</summary>
    ShrugLoss,
    /// <summary>아픈 손실. 같이 아파한다.</summary>
    Sting,
    /// <summary>손실률은 처참한데 금액은 작다.</summary>
    PctWreck,
    /// <summary>손실률은 얇은데 금액이 크다. 판돈에 놀란다.</summary>
    StakeOuch,
    /// <summary>큰 손실. 놀라거나, 위로하거나, 쓴웃음을 짓는다.</summary>
    Disaster
}

/// <summary>카드 한 장을 한 사람이 읽은 결과.</summary>
public struct ChatProofRead
{
    public bool gain, holding;
    /// <summary>amount: 손익금의 크기(부호 없음). percent: 수익률의 크기(%). stake: 들어간 돈(매수 원금).</summary>
    public decimal amount, percent, stake;
    /// <summary>rawTier: 금액만 본 단. tier: 보는 사람의 재산에 견준 단.</summary>
    public ChatProofAmountTier rawTier, tier;
    public ChatProofPercentTier percentTier;
    public ChatProofMood mood;
}

public static class ChatProofMath
{
    // ── 문턱값. 바꿀 때는 Assets/Editor/ChatProofCheck.cs의 표가 그대로 통과하는지 본다. ──
    /// <summary>금액 단의 윗선(돈): 푼돈, 밥값, 괜찮은 하루, 월급, 차 한 대. 그 위는 인생이 바뀌는 돈.</summary>
    public const decimal PocketMax = 10_000m, LunchMax = 1_000_000m, GoodDayMax = 10_000_000m, SalaryMax = 100_000_000m, CarMax = 1_000_000_000m;
    /// <summary>수익률 단의 윗선(%): 얇다, 쏠쏠, 크다. 그 위는 엄청나다.</summary>
    public const decimal ThinMax = 3m, SolidMax = 15m, BigMax = 50m;
    /// <summary>보는 사람의 재산에 견줘서: 이 몫에도 못 미치면 한 단 덜 놀라고, 이 몫을 넘으면 한 단 더 놀란다.</summary>
    public const decimal UnimpressedShare = .001m, OverwhelmedShare = 1m;
    /// <summary>보유 인증: 들고 있는 크기가 이만큼이면 손익과 상관없이 그 크기에 먼저 놀란다. 이만큼도 안 되면 손익이 얼마든 푼돈 취급이다.</summary>
    public const decimal HugeStake = 1_000_000_000m, TinyStake = 100_000m;

    private static readonly ChatProofMood[,] GainMoods =
    {
        //                 얇다                      쏠쏠                      크다                      엄청나다
        /* 푼돈 */      { ChatProofMood.TeaseSmall, ChatProofMood.TeaseSmall, ChatProofMood.PctNoSeed, ChatProofMood.PctNoSeed },
        /* 밥값 */      { ChatProofMood.Modest,     ChatProofMood.Modest,     ChatProofMood.PctNoSeed, ChatProofMood.PctNoSeed },
        /* 괜찮은 하루 */ { ChatProofMood.Modest,     ChatProofMood.Modest,     ChatProofMood.Envy,      ChatProofMood.Envy },
        /* 월급 */      { ChatProofMood.StakeAwe,   ChatProofMood.Envy,       ChatProofMood.Envy,      ChatProofMood.Envy },
        /* 차 한 대 */   { ChatProofMood.StakeAwe,   ChatProofMood.Shock,      ChatProofMood.Shock,     ChatProofMood.Shock },
        /* 인생 */      { ChatProofMood.StakeAwe,   ChatProofMood.Shock,      ChatProofMood.Shock,     ChatProofMood.Shock }
    };
    private static readonly ChatProofMood[,] LossMoods =
    {
        /* 푼돈 */      { ChatProofMood.ShrugLoss,  ChatProofMood.ShrugLoss,  ChatProofMood.PctWreck,  ChatProofMood.PctWreck },
        /* 밥값 */      { ChatProofMood.ShrugLoss,  ChatProofMood.Sting,      ChatProofMood.PctWreck,  ChatProofMood.PctWreck },
        /* 괜찮은 하루 */ { ChatProofMood.Sting,      ChatProofMood.Sting,      ChatProofMood.Sting,     ChatProofMood.Sting },
        /* 월급 */      { ChatProofMood.StakeOuch,  ChatProofMood.Sting,      ChatProofMood.Sting,     ChatProofMood.Sting },
        /* 차 한 대 */   { ChatProofMood.StakeOuch,  ChatProofMood.Disaster,   ChatProofMood.Disaster,  ChatProofMood.Disaster },
        /* 인생 */      { ChatProofMood.StakeOuch,  ChatProofMood.Disaster,   ChatProofMood.Disaster,  ChatProofMood.Disaster }
    };

    public static ChatProofAmountTier AmountTier(decimal amount)
    {
        amount = Math.Abs(amount);
        return amount < PocketMax ? ChatProofAmountTier.Pocket : amount < LunchMax ? ChatProofAmountTier.Lunch : amount < GoodDayMax ? ChatProofAmountTier.GoodDay :
            amount < SalaryMax ? ChatProofAmountTier.Salary : amount < CarMax ? ChatProofAmountTier.Car : ChatProofAmountTier.Life;
    }
    public static ChatProofPercentTier PercentTier(decimal percent)
    {
        percent = Math.Abs(percent);
        return percent < ThinMax ? ChatProofPercentTier.Thin : percent < SolidMax ? ChatProofPercentTier.Solid : percent < BigMax ? ChatProofPercentTier.Big : ChatProofPercentTier.Huge;
    }
    /// <summary>보는 사람의 재산에 견준 금액 단. 푼돈은 가난한 사람이 봐도 푼돈이고(올리지 않는다), 재산을 모르면(0) 금액만 본다.</summary>
    public static ChatProofAmountTier Relative(ChatProofAmountTier tier, decimal amount, decimal viewerWealth)
    {
        if (viewerWealth <= 0m) { return tier; }
        decimal share = Math.Abs(amount) / viewerWealth;
        if (share < UnimpressedShare && tier > ChatProofAmountTier.Pocket) { return tier - 1; }
        if (share >= OverwhelmedShare && tier > ChatProofAmountTier.Pocket && tier < ChatProofAmountTier.Life) { return tier + 1; }
        return tier;
    }

    /// <summary>카드를 읽는다. viewerWealth는 보는 사람의 재산(모르면 0).</summary>
    public static ChatProofRead Read(ChatProfitCard card, decimal viewerWealth)
    {
        if (card == null) { return default; }
        decimal stake = card.quantity > 0m && card.averageCost > 0m ? card.quantity * card.averageCost : card.percent != 0m ? Math.Abs(card.amount / card.percent * 100m) : 0m;
        return Read(card.kind == ChatProfitCardKind.Holding, card.amount, card.percent, stake, viewerWealth);
    }
    /// <summary>amount는 부호 있는 손익금, percent는 부호 있는 수익률(%), stake는 들어간 돈(모르면 0).</summary>
    public static ChatProofRead Read(bool holding, decimal amount, decimal percent, decimal stake, decimal viewerWealth)
    {
        var read = new ChatProofRead { gain = amount >= 0m, holding = holding, amount = Math.Abs(amount), percent = Math.Abs(percent), stake = Math.Max(0m, stake) };
        read.rawTier = AmountTier(amount); read.tier = Relative(read.rawTier, amount, viewerWealth); read.percentTier = PercentTier(percent);
        read.mood = (read.gain ? GainMoods : LossMoods)[(int)read.tier, (int)read.percentTier];
        if (holding && read.stake > 0m)
        {
            // 들고 있는 크기가 아주 크면 손익보다 그 크기가 먼저 눈에 들어온다(보는 사람이 그보다 훨씬 부자가 아니라면).
            bool hugeStake = read.stake >= HugeStake && (viewerWealth <= 0m || read.stake >= viewerWealth * UnimpressedShare * 100m);
            if (hugeStake && read.gain && read.mood != ChatProofMood.Shock) { read.mood = ChatProofMood.StakeAwe; }
            else if (hugeStake && !read.gain && read.mood != ChatProofMood.Disaster) { read.mood = ChatProofMood.StakeOuch; }
            // 들고 있는 것 자체가 푼돈이면 수익률이 얼마든 "그걸 인증이라고" 쪽이다.
            else if (read.stake < TinyStake && read.tier == ChatProofAmountTier.Pocket) { read.mood = read.gain ? (read.percentTier >= ChatProofPercentTier.Big ? ChatProofMood.PctNoSeed : ChatProofMood.TeaseSmall) : (read.percentTier >= ChatProofPercentTier.Big ? ChatProofMood.PctWreck : ChatProofMood.ShrugLoss); }
        }
        return read;
    }

    /// <summary>놀라거나 부러워하는 기분인가(푼돈 카드에 이런 말이 달리면 잘못이다).</summary>
    public static bool Awed(ChatProofMood mood) => mood == ChatProofMood.StakeAwe || mood == ChatProofMood.Envy || mood == ChatProofMood.Shock || mood == ChatProofMood.StakeOuch || mood == ChatProofMood.Disaster;
    /// <summary>금액이 작다고 놀리는 기분인가(큰돈 카드에 이런 말이 달리면 잘못이다).</summary>
    public static bool TeasesSmall(ChatProofMood mood) => mood == ChatProofMood.TeaseSmall || mood == ChatProofMood.PctNoSeed || mood == ChatProofMood.ShrugLoss || mood == ChatProofMood.PctWreck;
    /// <summary>예전의 말투별 대사(축하·위로)를 섞어 써도 어울리는 기분인가. 놀림이나 판돈 얘기에는 "+1% ㄷㄷ"·"힘내" 같은 말이 어울리지 않는다.</summary>
    public static bool PlainFits(ChatProofMood mood) => mood == ChatProofMood.Modest || mood == ChatProofMood.Envy || mood == ChatProofMood.Shock || mood == ChatProofMood.Sting || mood == ChatProofMood.Disaster;
}

/// <summary>금액과 수익률을 그 언어의 말투로 적는다. 채팅의 다른 곳과 같은 꼴이다(ChatSocialNumbers, LocalizedChat).</summary>
public static class ChatProofText
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    /// <summary>앞의 두 자리만 남기고 버린다(53,400,000 → 53,000,000). 말로 할 때는 그 정도만 말한다.</summary>
    private static decimal TwoDigits(decimal value)
    {
        decimal step = 1m;
        while (step * 100m <= value) { step *= 10m; }
        return Math.Floor(value / step) * step;
    }
    /// <summary>
    /// 금액(부호 없음). 한국어 "5,300만돈"·"3천돈"·"500돈"·"3.2억돈", 영어 "Ð53m"·"Ð3k"·"Ð500"·"Ð3.2b", 일본어 "5300万ドン"·"3000ドン"·"3.2億ドン".
    /// </summary>
    public static string Amount(string code, decimal amount)
    {
        decimal value = TwoDigits(Math.Abs(amount));
        if (code == "en")
        {
            decimal scale = value >= 1_000_000_000m ? 1_000_000_000m : value >= 1_000_000m ? 1_000_000m : value >= 1_000m ? 1_000m : 1m;
            return "Ð" + (value / scale).ToString("0.#", Culture) + (scale == 1_000_000_000m ? "b" : scale == 1_000_000m ? "m" : scale == 1_000m ? "k" : "");
        }
        if (code == "ja")
        {
            if (value >= 100_000_000m) { return (value / 100_000_000m).ToString("0.#", Culture) + "億ドン"; }
            if (value >= 10_000m) { return (value / 10_000m).ToString("0", Culture) + "万ドン"; }
            return value.ToString("0", Culture) + "ドン";
        }
        if (value >= 100_000_000m) { return (value / 100_000_000m).ToString("0.#", Culture) + "억돈"; }
        if (value >= 10_000m) { return (value / 10_000m).ToString("#,##0", Culture) + "만돈"; }
        if (value >= 1_000m && value % 1_000m == 0m) { return (value / 1_000m).ToString("0", Culture) + "천돈"; }
        return value.ToString("#,##0", Culture) + "돈";
    }
    /// <summary>수익률(부호 없음). 한국어 "1퍼"·"0.5퍼"·"80퍼", 영어와 일본어 "1%"·"0.5%"·"80%".</summary>
    public static string Percent(string code, decimal percent)
    {
        decimal size = Math.Abs(percent);
        string number = Math.Round(size, size < 10m ? 1 : 0, MidpointRounding.AwayFromZero).ToString("0.#", Culture);
        return number + (code == "ko" ? "퍼" : "%");
    }
    public static Dictionary<string, string> Slots(string code, in ChatProofRead read, string stockName)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal) { ["amount"] = Amount(code, read.amount), ["pct"] = Percent(code, read.percent) };
        if (!string.IsNullOrEmpty(stockName)) { slots["stockName"] = stockName; }
        if (read.stake > 0m) { slots["stake"] = Amount(code, read.stake); }
        return slots;
    }
}

[Serializable] public sealed class ChatProofLocale
{
    public int schemaVersion = 1;
    /// <summary>숫자가 든 문장과 없는 문장이 다 있을 때 숫자가 든 쪽을 고를 확률. 대개는 숫자 없이 말한다.</summary>
    public double numberLineChance = .3;
    public ChatMarketGroup[] groups;
}

/// <summary>
/// 언어별 인증 반응 대사(Chat/chat_proof.ko / .en / .ja).
/// 묶음 이름은 기분의 이름이다(TeaseSmall, Shock …). 덧붙는 묶음: ".hold"(보유 인증에만), ".sold"(실현 인증에만), ".edge"(놀림·쓴웃음: 속상해하는 사람에게는 쓰지 않는다).
/// </summary>
public static class ChatProofAssets
{
    private static Dictionary<string, Dictionary<string, string[]>> locales;
    private static Dictionary<string, double> numberChance;
    private static readonly string[] None = Array.Empty<string>();
    /// <summary>언어 코드로 파일 내용을 돌려주는 함수. 비워 두면 Resources에서 읽는다(에디터 점검 도구는 파일에서 바로 읽는다).</summary>
    public static Func<string, string> Reader;
    public static readonly List<string> Problems = new();
    private static readonly List<string> plain = new(), numbered = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { locales = null; numberChance = null; Reader = null; Problems.Clear(); }

    public static void Reload()
    {
        var loaded = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.Ordinal); var chances = new Dictionary<string, double>(StringComparer.Ordinal); Problems.Clear();
        foreach (string code in ChatHypeAssets.Languages)
        {
            try
            {
                string json = Reader != null ? Reader(code) : Resources.Load<TextAsset>("Chat/chat_proof." + code)?.text;
                if (string.IsNullOrEmpty(json)) { Problems.Add(code + ": missing chat_proof." + code); continue; }
                var data = JsonUtility.FromJson<ChatProofLocale>(json);
                if (data == null || data.schemaVersion != 1 || data.groups == null || data.groups.Length == 0) { throw new ArgumentException("Invalid chat_proof schema"); }
                var index = new Dictionary<string, string[]>(StringComparer.Ordinal);
                foreach (var group in data.groups)
                {
                    if (group == null || string.IsNullOrEmpty(group.key) || group.texts == null || group.texts.Length == 0 || index.ContainsKey(group.key)) { throw new ArgumentException("Invalid/duplicate chat_proof group: " + group?.key); }
                    foreach (string text in group.texts) { if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("Empty chat_proof text: " + group.key); } }
                    index[group.key] = group.texts;
                }
                // 기분마다 대사가 있어야 한다. 하나라도 빠지면 그 기분에서만 예전 대사가 섞여 나온다.
                foreach (ChatProofMood mood in Enum.GetValues(typeof(ChatProofMood))) { if (!index.ContainsKey(mood.ToString())) { throw new ArgumentException("Missing chat_proof lines: " + mood); } }
                loaded[code] = index; chances[code] = data.numberLineChance;
            }
            catch (ArgumentException ex) { Problems.Add(code + ": " + ex.Message); Debug.LogWarning("인증 반응 대사를 읽지 못했습니다(" + code + "). 이 언어는 예전 대사로 반응합니다: " + ex.Message); }
        }
        locales = loaded; numberChance = chances;
    }
    public static bool Has(string code) { if (locales == null) { Reload(); } return locales.ContainsKey(code ?? ""); }
    public static string[] Lines(string code, string key)
    {
        if (locales == null) { Reload(); }
        return locales.TryGetValue(code ?? "", out var index) && index.TryGetValue(key, out var texts) ? texts : None;
    }
    private static bool Question(string line) { string trimmed = line.TrimEnd(' ', 'ㅋ', 'ㄷ', 'w', 'ｗ', '草'); return trimmed.EndsWith("?", StringComparison.Ordinal) || trimmed.EndsWith("？", StringComparison.Ordinal); }

    /// <summary>
    /// 이 카드를 이렇게 읽은 사람의 한마디(빈칸은 아직 안 채운 틀). 없으면 null.
    ///   gentle: 놀림·쓴웃음(.edge)을 빼고 고른다(속상해하는 사람, 위로하려는 사람).
    ///   question: true면 묻는 말만, false면 묻지 않는 말만, null이면 가리지 않는다(대화 틀의 "묻기" 차례에는 묻는 말이 나와야 답이 이어진다).
    ///   recent: 방금 나온 문장인가(그런 문장은 피한다). key에는 고른 묶음의 이름이 담긴다("proof.StakeAwe").
    /// 메인 스레드에서만 부른다.
    /// </summary>
    public static string Pick(string code, in ChatProofRead read, bool gentle, bool? question, IReadOnlyDictionary<string, string> slots, Func<double> random, Func<string, bool> recent, out string key)
    {
        key = "proof." + read.mood;
        if (!Has(code)) { return null; }
        string name = read.mood.ToString();
        for (int pass = 0; pass < 2; pass++)
        {
            plain.Clear(); numbered.Clear();
            Collect(Lines(code, name), slots, question, pass == 0 ? recent : null);
            Collect(Lines(code, name + (read.holding ? ".hold" : ".sold")), slots, question, pass == 0 ? recent : null);
            if (!gentle) { Collect(Lines(code, name + ".edge"), slots, question, pass == 0 ? recent : null); }
            if (plain.Count + numbered.Count > 0) { break; }
        }
        if (plain.Count + numbered.Count == 0) { return null; }
        double chance = numberChance != null && numberChance.TryGetValue(code, out double value) ? value : .3;
        var from = numbered.Count > 0 && (plain.Count == 0 || random() < chance) ? numbered : plain;
        return from[(int)(random() * from.Count) % from.Count];
    }
    private static void Collect(string[] lines, IReadOnlyDictionary<string, string> slots, bool? question, Func<string, bool> recent)
    {
        foreach (string line in lines)
        {
            if (!ChatReactionEngine.Fillable(line, slots) || recent != null && recent(line) || question.HasValue && Question(line) != question.Value) { continue; }
            (line.IndexOf("{amount}", StringComparison.Ordinal) >= 0 || line.IndexOf("{pct}", StringComparison.Ordinal) >= 0 || line.IndexOf("{stake}", StringComparison.Ordinal) >= 0 ? numbered : plain).Add(line);
        }
    }
}

public sealed partial class InvestorSociety
{
    /// <summary>인증 반응에서 방금 쓴 문장들. 같은 카드에 같은 말이 두 번 달리지 않게 한다.</summary>
    private static readonly Queue<string> proofRecent = new();
    private static bool ProofRecent(string line) => proofRecent.Contains(line);
    private static void NoteProofLine(string line) { proofRecent.Enqueue(line); while (proofRecent.Count > 10) { proofRecent.Dequeue(); } }

    /// <summary>
    /// 이 봇이 이 카드를 보고 할 한마디(빈칸까지 채운 글). 그 언어의 대사가 없거나 고를 것이 없으면 null(예전 대사로 간다).
    /// 금액은 이 봇의 재산에 견줘 읽는다: 부자는 덜 놀란다.
    /// </summary>
    private static string ProofLine(TradeBot viewer, ChatProfitCard card, bool gentle, bool? question, Func<double> random, out string key, out ChatProofRead read, bool measured = false)
    {
        key = null; read = default;
        if (card == null || viewer == null) { return null; }
        string code = GameLocale.Code;
        read = ChatProofMath.Read(card, viewer.Equity());
        var slots = ChatProofText.Slots(code, read, GameLocale.Text(card.stockName ?? card.symbol ?? ""));
        string line = ChatProofAssets.Pick(code, read, gentle, question, slots, random, ProofRecent, out key);
        if (line == null) { return null; }
        NoteProofLine(line);
        return ChatHypeAssets.Fill(VoicedProof(line, code, read, gentle, measured, SpeechHabit(viewer.Social.Id), random), slots);
    }
    /// <summary>
    /// 인증 반응에 말맛을 입힌다(ChatVoice.cs). 세기는 금액 단에서 온다: 5,000만돈 카드에는 "ㄷㄷㄷㄷㄷㄷ", 밥값 카드에는 "ㄷㄷ".
    /// 위로하는 말(gentle)과 새벽 관찰실의 말(measured), 남의 손실을 두고 하는 말에는 말머리를 달지 않고 대문자로 소리치지도 않는다
    /// ("아니 밥은 꼭 먹어", "HOW DID IT GET THAT FAR?"는 위로가 아니다). 새벽 관찰실에서는 ㅋ·ㄷ 줄도 가장 짧게 친다.
    /// </summary>
    public static string VoicedProof(string line, string code, in ChatProofRead read, bool gentle, bool measured, uint habit, Func<double> random)
    {
        var voice = ChatReactionAssets.Voice(code);
        if (voice == null) { return line; }
        // 위로는 소리치지 않는다: 금액이 아무리 커도 한 단(세기 1)까지만 친다.
        int level = measured ? ChatVoice.Mild : gentle ? Math.Min(ChatVoice.Warm, ChatVoice.ProofLevel(read)) : ChatVoice.ProofLevel(read);
        return gentle || measured || !read.gain ? ChatVoice.After(line, voice, level, random, false) : ChatVoice.Apply(line, voice, level, ChatVoice.ProofLean(read), habit, random);
    }

    // ── 맞장구 ──
    /// <summary>방금 쓴 맞장구들(같은 한마디가 연달아 뜨지 않게), 다음 맞장구를 해도 되는 시각, 글마다 정해 둔 맞장구(같은 글을 다시 그려도 같은 말이 나오게).</summary>
    private readonly Queue<string> backRecent = new();
    private readonly Dictionary<long, string> backSaid = new();
    private double backNextAt;
    /// <summary>확인용: 지금까지 한 맞장구의 수.</summary>
    public int BackchannelLines { get; private set; }
    /// <summary>
    /// 지금 라운지에서 한두 마디 맞장구를 해도 되는가. 맞장구 사이에는 틈(backGapSec)을 둔다: 방이 한 글자짜리 말로 차지 않게.
    /// 급등락 군중·차트 수다·대화 틀의 답글이 모두 이 한 곳에서 허락을 받는다. 시계가 뒤로 갔으면(불러오기) 틈을 잊는다.
    /// </summary>
    private bool BackDue(ChatVoiceRules voice, double now)
    {
        if (voice == null || voice.backChance <= 0) { return false; }
        if (backNextAt - now > voice.backGapSec * 2) { backNextAt = 0; }
        return now >= backNextAt;
    }
    /// <summary>맞장구 한마디를 고른다. 골랐으면 다음 맞장구까지의 틈을 건다.</summary>
    private string TakeBack(ChatVoiceRules voice, string mood, int level, Func<double> random, double now)
    {
        string line = ChatVoice.Back(voice, mood, level, random, backRecent.Contains);
        if (line == null) { return null; }
        backRecent.Enqueue(line); while (backRecent.Count > 3) { backRecent.Dequeue(); }
        backNextAt = now + voice.backGapSec; BackchannelLines++;
        return line;
    }
    /// <summary>
    /// 라운지에서 다른 봇의 말에 다는 맞장구(대화 틀의 AGREE)를 가끔 한두 마디로 바꾼다("ㄹㅇ", "그니까", "fr", "それな"). 바꿨으면 true.
    /// 새벽 관찰실에서는 하지 않는다(그 방의 맞장구는 까닭을 보탠다: ChatDawnRoom.cs). 존댓말 봇, 약속·신호·기억·공지에 딸린 말, 인증 카드에 다는 말도 그대로 둔다.
    /// 번역을 거치지 않으므로 번역용 정보는 비운다.
    /// </summary>
    private static bool TryBackchannel(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, out string text)
    {
        text = null;
        if (parent == null || message.roomId != 0 || message.act != SocialAct.AGREE || parent.profitCard != null || template.style == "formal" || !CommunityAllowed(template, message)) { return false; }
        var society = Instance; string code = GameLocale.Code; var voice = ChatReactionAssets.Voice(code);
        if (society == null || voice == null || !society.SocialBots.ContainsKey(parent.senderId)) { return false; }
        // 같은 글을 다시 그릴 때는 처음 정한 대로 한다.
        if (society.backSaid.TryGetValue(message.id, out string known)) { text = known; }
        else
        {
            double now = society.SocialTime.Seconds;
            var random = CosmeticRandom(mind, message, 0x6ac1e5b3u);
            if (random.Next() < voice.backChance && society.BackDue(voice, now))
            {
                string mood = parent.act == SocialAct.LAMENT ? "sad" : parent.act == SocialAct.BRAG ? "awe" : parent.act == SocialAct.MOCK ? "laugh" : "agree";
                text = society.TakeBack(voice, mood, ChatVoice.Warm, random.Next, now);
                if (text != null) { message.debug.speechKey = "back." + mood; }
            }
            if (society.backSaid.Count > 256) { society.backSaid.Clear(); }
            society.backSaid[message.id] = text;
        }
        if (text == null) { return false; }
        message.debug.numberStyle = "맞장구(" + code + ")";
        message.localizedPhrase = null; message.localizedValues = null; message.localizedSuffix = ""; message.localizedTarget = null;
        return true;
    }

    /// <summary>
    /// 봇의 인증 카드가 붙은 말에 다른 봇이 다는 답글(대화 틀)을 금액·수익률에 맞는 말로 바꾼다. 바꿨으면 true.
    /// 대상: 맞장구·되묻기·의심·놀림·위로·한마디. 묻는 차례(ASK)에는 묻는 말만 고른다(그래야 답이 이어진다).
    /// 번역을 거치지 않으므로 번역용 정보는 비운다.
    /// </summary>
    private static bool TryProofReply(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, out string text)
    {
        text = null;
        var card = parent?.profitCard; var facts = message.facts;
        if (card == null || facts == null || facts.signal || message.memory || template.variant == "memory" || template.promisedAction != SocialPromisedAction.None || !string.IsNullOrEmpty(facts.notice)) { return false; }
        var act = message.act;
        if (act != SocialAct.AGREE && act != SocialAct.ASK && act != SocialAct.DOUBT && act != SocialAct.MOCK && act != SocialAct.COMFORT && act != SocialAct.REACT) { return false; }
        var society = Instance;
        if (society == null || !society.SocialBots.TryGetValue(mind.SocialId, out var viewer)) { return false; }
        var random = CosmeticRandom(mind, message, 0x1f83d9abu);
        // 새벽 관찰실에 뜨는 인증 반응은 차분하게 친다(말머리 없이, ㅋ·ㄷ 줄은 가장 짧게). 맞장구도 라운지에서만 한다.
        bool dawn = message.roomId == NpcLeadingRoom.RoomId;
        string line = ProofLine(viewer, card, act == SocialAct.COMFORT, act == SocialAct.ASK, random.Next, out string key, out var read, dawn);
        if (line == null) { return false; }
        // 카드에 다는 맞장구·한마디는 가끔 정말 한두 마디다("ㄷㄷ", "ㅋㅋㅋㅋ", "ㅠㅠ"). 무엇으로 맞장구치는지는 카드의 기분이 정한다:
        // 큰돈에는 놀라고, 푼돈에는 웃고, 손실에는 같이 아파한다. 세기는 금액 단을 따른다.
        var voice = ChatReactionAssets.Voice(GameLocale.Code);
        if (!dawn && message.roomId == 0 && (act == SocialAct.AGREE || act == SocialAct.REACT) && voice != null && template.style != "formal")
        {
            string mood = ChatProofMath.TeasesSmall(read.mood) ? "laugh" : !read.gain ? "sad" : ChatProofMath.Awed(read.mood) ? "awe" : "agree";
            // 같은 글을 다시 그릴 때는 처음 정한 대로 한다.
            if (!society.backSaid.TryGetValue(message.id, out string back))
            {
                double now = society.SocialTime.Seconds;
                if (random.Next() < voice.backChance && society.BackDue(voice, now)) { back = society.TakeBack(voice, mood, ChatVoice.ProofLevel(read), random.Next, now); }
                if (society.backSaid.Count > 256) { society.backSaid.Clear(); }
                society.backSaid[message.id] = back;
            }
            if (back != null) { line = back; key = "back." + mood; }
        }
        text = line;
        message.debug.speechKey = key; message.debug.numberStyle = "인증 반응(" + GameLocale.Code + ")";
        message.localizedPhrase = null; message.localizedValues = null; message.localizedSuffix = ""; message.localizedTarget = null;
        return true;
    }
}
