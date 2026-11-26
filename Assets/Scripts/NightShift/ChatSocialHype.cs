using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// 급등·급락 폭주 연출, 개소리 봇, 끊어 치기 습관.
// 여기서 나가는 말은 화면 표시 전용이다. 방의 최근 대화·목격·감정·기억·난수열 어디에도 들어가지 않으므로
// 어떤 봇의 판단도 바꾸지 않는다. 재료는 누구나 보는 시세와 체결뿐이고 진짜 가치는 읽지 않는다.
// 대사와 끊는 규칙은 언어마다 따로 쓴 파일(chat_hype.ko/en/ja)에서 온다. 서로의 번역이 아니다.

[Serializable] public sealed class ChatHypeLines { public string key, holding, tone; public string[] texts; }
[Serializable] public sealed class ChatHypeSetup
{
    public string key, when, lean, text;
    /// <summary>다음에 나왔을 때 "아까 그 얘기"로 되짚을 짧은 이름. 없으면 되짚지 않는다.</summary>
    public string topic;
    /// <summary>결론을 붙이지 않고 혼자 끝나는 말.</summary>
    public bool solo;
}
[Serializable] public sealed class ChatHypeVerdict { public string key, lean, text; public bool predict; }
[Serializable] public sealed class ChatHypeDocument { public int schemaVersion = 1; public SocialWeight[] settings; }
[Serializable] public sealed class ChatHypeLocale
{
    public int schemaVersion = 1;
    /// <summary>{h} 시, {m} 분, {mm} 두 자리 분.</summary>
    public string clockFormat = "{h}:{mm}";
    public ChatBreathRules breath;
    public ChatHypeLines[] crowd, echoes;
    public ChatHypeSetup[] setups;
    public ChatHypeVerdict[] verdicts;
    /// <summary>지난번 개소리를 되짚는 말. {topic}이 들어간다.</summary>
    public string[] callbacks;
    [NonSerialized] public Dictionary<string, string[]> crowdIndex, echoIndex;
}

/// <summary>chat_hype.json(수치)과 언어별 대사 파일. 읽기에 실패하면 이 기능만 꺼지고 기존 채팅은 그대로 돈다.</summary>
public static class ChatHypeAssets
{
    public static readonly string[] Languages = { "ko", "en", "ja" };
    public static readonly string[] Phases = { "discover", "flood", "heat", "break", "crashDiscover", "crashFlood", "crashHeat", "crashBreak" };
    public static readonly string[] Moments = { "surge", "break", "crash", "playerBuy", "playerDip", "playerSell", "round", "lull", "open", "close" };
    public static readonly string[] EchoKinds = { "first", "again", "right", "wrong", "gloat" };
    private static readonly string[] RequiredSettings =
    {
        "hypeEnabled", "sampleSec", "windowSec", "surgeRisePct", "crashDropPct", "breakDropPct", "discoverSec", "floodSec", "heatSec", "breakSec", "breakQuietSec", "hypeCooldownSec",
        "discoverGapMin", "discoverGapMax", "floodGapMin", "floodGapMax", "heatGapMin", "heatGapMax", "breakGapMin", "breakGapMax", "crowdBotCooldownSec", "crowdRepeatWindow",
        "oddballEnabled", "oddballWarmupSec", "oddballCooldownSec", "oddballSurgeChance", "oddballBreakChance", "oddballCrashChance", "oddballPlayerChance", "oddballRoundChance",
        "oddballLullChance", "oddballOpenChance", "oddballCloseChance", "oddballCallbackChance", "oddballFloodLines", "oddballHushSec", "oddballSecondEchoChance", "oddballMaxPieces",
        "predictMinMinutes", "predictMaxMinutes", "predictWrongCallChance", "playerBigShare", "playerWindowSec", "playerDelayMinSec", "playerDelayMaxSec", "playerChaseRisePct", "playerDipDropPct",
        "roundRestSec", "lullSec", "lullMovePct", "openMinutes", "closeMinutes",
        "breathEnabled", "breathHeavyShare", "breathLightShare", "breathHeavyChance", "breathLightChance", "breathMaxPieces"
    };
    // dayPct·fromHigh·fromLow는 부호와 %가 붙어서 들어온다("+490%"). retrace·gainLeft도 %가 붙는다("15%"). 대사에서 %를 또 붙이지 않는다.
    // movePct만 예전 그대로 숫자만 들어온다("+12.3")(대사가 "{movePct}%"로 쓴다).
    private static readonly HashSet<string> KnownSlots = new() { "stockName", "price", "movePct", "digit", "nameLen", "count", "clock", "minutes", "round", "topic",
        "dayPct", "fromHigh", "fromLow", "retrace", "gainLeft", "dayTalk", "multTalk", "bagger", "dayHigh", "dayLow" };
    private static readonly Regex SlotPattern = new(@"\{([A-Za-z]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly string[] None = Array.Empty<string>();
    private static Dictionary<string, double> values;
    private static Dictionary<string, ChatHypeLocale> locales;
    private static bool failed;
    public static bool Ready
    {
        get
        {
            if (locales == null && !failed)
            {
                try { Reload(); }
                catch (ArgumentException ex) { failed = true; Debug.LogWarning("채팅 폭주·개소리 봇·끊어 치기를 끕니다: " + ex.Message); }
            }
            return locales != null;
        }
    }
    public static void Reload()
    {
        var settings = Resources.Load<TextAsset>("Chat/chat_hype");
        if (settings == null) { throw new ArgumentException("Missing chat_hype"); }
        var loaded = new Dictionary<string, ChatHypeLocale>(StringComparer.Ordinal);
        foreach (string code in Languages)
        {
            var asset = Resources.Load<TextAsset>("Chat/chat_hype." + code);
            if (asset != null) { loaded[code] = JsonUtility.FromJson<ChatHypeLocale>(asset.text); }
        }
        Install(JsonUtility.FromJson<ChatHypeDocument>(settings.text), loaded);
    }
    public static void Install(ChatHypeDocument document, Dictionary<string, ChatHypeLocale> loaded)
    {
        if (document == null || document.schemaVersion != 1 || document.settings == null || loaded == null || loaded.Count == 0) { throw new ArgumentException("Invalid chat_hype schema"); }
        var nextValues = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var row in document.settings)
        {
            if (row == null || string.IsNullOrEmpty(row.key) || double.IsNaN(row.value) || double.IsInfinity(row.value) || row.value < 0 || nextValues.ContainsKey(row.key))
            { throw new ArgumentException("Invalid/duplicate chat_hype setting"); }
            nextValues.Add(row.key, row.value);
        }
        foreach (string key in RequiredSettings) { if (!nextValues.ContainsKey(key)) { throw new ArgumentException("Missing chat_hype setting: " + key); } }
        foreach (string gap in new[] { "discover", "flood", "heat", "break" })
        { if (nextValues[gap + "GapMin"] <= 0 || nextValues[gap + "GapMax"] < nextValues[gap + "GapMin"]) { throw new ArgumentException("Invalid chat_hype gap: " + gap); } }
        if (nextValues["sampleSec"] <= 0 || nextValues["windowSec"] < nextValues["sampleSec"] * 3 || nextValues["surgeRisePct"] <= 0 || nextValues["crashDropPct"] <= 0 || nextValues["breakDropPct"] <= 0 ||
            nextValues["predictMinMinutes"] < 1 || nextValues["predictMaxMinutes"] < nextValues["predictMinMinutes"] || nextValues["playerDelayMaxSec"] < nextValues["playerDelayMinSec"] ||
            nextValues["breathMaxPieces"] < 2 || nextValues["oddballMaxPieces"] < 2)
        { throw new ArgumentException("Invalid chat_hype window/threshold"); }
        foreach (var pair in loaded) { Validate(pair.Key, pair.Value); }
        values = nextValues; locales = loaded; failed = false;
    }
    private static void Validate(string code, ChatHypeLocale locale)
    {
        if (locale == null || locale.schemaVersion != 1 || locale.breath == null || locale.crowd == null || locale.echoes == null || locale.setups == null || locale.verdicts == null || string.IsNullOrEmpty(locale.clockFormat))
        { throw new ArgumentException("Invalid chat_hype." + code + " schema"); }
        if (locale.breath.minPiece < 1 || locale.breath.minTotal < locale.breath.minPiece * 2 || locale.breath.gapBaseSec < 0 || locale.breath.gapPerCharSec < 0 || locale.breath.gapMaxSec <= 0)
        { throw new ArgumentException("Invalid breath rules: " + code); }
        locale.crowdIndex = Group(code, locale.crowd, true);
        locale.echoIndex = Group(code, locale.echoes, false);
        foreach (string phase in Phases) { if (!locale.crowdIndex.ContainsKey(phase + "/any")) { throw new ArgumentException("Missing crowd lines: " + code + " " + phase); } }
        foreach (string kind in EchoKinds) { if (!locale.echoIndex.ContainsKey(kind)) { throw new ArgumentException("Missing echo lines: " + code + " " + kind); } }
        var keys = new HashSet<string>();
        foreach (var verdict in locale.verdicts)
        {
            if (verdict == null || string.IsNullOrEmpty(verdict.key) || !keys.Add("v:" + verdict.key) || !Lean(verdict.lean) || verdict.predict && verdict.lean == "any") { throw new ArgumentException("Invalid verdict: " + code); }
            CheckSlots(code, verdict.text);
        }
        foreach (var setup in locale.setups)
        {
            if (setup == null || string.IsNullOrEmpty(setup.key) || !keys.Add("s:" + setup.key) || !Lean(setup.lean) || Array.IndexOf(Moments, setup.when) < 0) { throw new ArgumentException("Invalid setup: " + code + " " + setup?.key); }
            CheckSlots(code, setup.text);
            if (setup.text.Contains("{round}") && setup.when != "round" || setup.text.Contains("{topic}")) { throw new ArgumentException("Misplaced slot in setup: " + code + " " + setup.key); }
        }
        foreach (string moment in Moments)
        {
            bool any = false; foreach (var setup in locale.setups) { any |= setup.when == moment; }
            if (!any) { throw new ArgumentException("Missing setups: " + code + " " + moment); }
        }
        if (locale.callbacks != null) { foreach (string text in locale.callbacks) { CheckSlots(code, text); } }
    }
    private static bool Lean(string lean) => lean == "up" || lean == "down" || lean == "any";
    private static Dictionary<string, string[]> Group(string code, ChatHypeLines[] rows, bool crowd)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row == null || string.IsNullOrEmpty(row.key) || row.texts == null || row.texts.Length == 0 ||
                crowd && (row.holding != "any" && row.holding != "held" && row.holding != "notHeld" && row.holding != "heldGain" && row.holding != "heldLoss" ||
                    !string.IsNullOrEmpty(row.tone) && row.tone != "formal" && row.tone != "greedy" && row.tone != "fearful" && row.tone != "cocky"))
            { throw new ArgumentException("Invalid line group: " + code + " " + row?.key); }
            foreach (string text in row.texts) { CheckSlots(code, text); }
            string key = crowd ? row.key + "/" + row.holding + (string.IsNullOrEmpty(row.tone) ? "" : "/" + row.tone) : row.key;
            if (result.ContainsKey(key)) { throw new ArgumentException("Duplicate line group: " + code + " " + key); }
            result.Add(key, row.texts);
        }
        return result;
    }
    private static void CheckSlots(string code, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("Empty text: " + code); }
        foreach (Match match in SlotPattern.Matches(text))
        { if (!KnownSlots.Contains(match.Groups[1].Value)) { throw new ArgumentException("Unknown slot: " + code + " " + match.Value); } }
    }
    public static double Get(string key) => values[key];
    /// <summary>지금 게임 언어의 대사. 그 언어 파일이 없으면 한국어, 그것도 없으면 있는 아무 언어.</summary>
    public static string LanguageCode
    {
        get
        {
            if (locales.ContainsKey(GameLocale.Code)) { return GameLocale.Code; }
            if (locales.ContainsKey("ko")) { return "ko"; }
            foreach (string code in locales.Keys) { return code; }
            return "ko";
        }
    }
    public static ChatHypeLocale Language(string code) => locales.TryGetValue(code, out var locale) ? locale : null;
    public static ChatHypeLocale Current => locales[LanguageCode];
    public static string[] Crowd(string phase, string holding, bool formal)
    {
        var index = Current.crowdIndex;
        if (formal && index.TryGetValue(phase + "/" + holding + "/formal", out var polite)) { return polite; }
        return index.TryGetValue(phase + "/" + holding, out var texts) ? texts : None;
    }
    /// <summary>
    /// 그 봇의 처지와 기질에 맞춰 쓴 군중 대사(없으면 빈 배열).
    /// holding: heldGain(들고 있고 수익 중) · heldLoss(들고 있고 물려 있음), tone: greedy(욕심) · fearful(겁) · cocky(큰소리).
    /// </summary>
    public static string[] CrowdOf(string phase, string holding, string tone)
        => Current.crowdIndex.TryGetValue(phase + "/" + holding + (string.IsNullOrEmpty(tone) ? "" : "/" + tone), out var texts) ? texts : None;
    public static string[] Echo(string kind) => Current.echoIndex.TryGetValue(kind, out var texts) ? texts : None;
    public static bool Fits(ChatHypeSetup setup, ChatHypeVerdict verdict) => setup.lean == "any" || verdict.lean == "any" || setup.lean == verdict.lean;
    /// <summary>종목 기호로 줄임말 하나를 돌려준다(없으면 null). 게임 쪽에서 연결한다.</summary>
    public static Func<string, string> ShortName;
    private const char NameMark = '\u0001';
    /// <summary>
    /// 틀의 빈칸을 채운다. 종목 이름은 절반쯤은 줄임말로 부른다(이름의 글자 수를 말하는 줄은 빼고).
    /// 한국어 이름 뒤의 조사(은/는, 이/가, 을/를, 과/와)는 실제로 들어간 이름의 받침에 맞춘다.
    /// </summary>
    public static string Fill(string text, IReadOnlyDictionary<string, string> slots)
    {
        if (!slots.TryGetValue("stockName", out string name) || string.IsNullOrEmpty(name) || text.IndexOf("{stockName}", StringComparison.Ordinal) < 0)
        { return SlotPattern.Replace(text, match => slots.TryGetValue(match.Groups[1].Value, out string value) ? value : ""); }
        if (slots.TryGetValue("stockShort", out string shorter) && !string.IsNullOrEmpty(shorter) && text.IndexOf("{nameLen}", StringComparison.Ordinal) < 0
            && CompanyBusiness.Hash(text + "|" + shorter + "|" + (slots.TryGetValue("price", out string price) ? price : "")) % 100 < 55)
        { name = shorter; }
        string marked = SlotPattern.Replace(text, match => match.Groups[1].Value == "stockName" ? NameMark.ToString() : slots.TryGetValue(match.Groups[1].Value, out string value) ? value : "");
        char last = name[name.Length - 1]; bool hangul = last >= '\uAC00' && last <= '\uD7A3', closed = hangul && (last - 0xAC00) % 28 != 0;
        var result = new System.Text.StringBuilder(marked.Length + name.Length * 2);
        for (int i = 0; i < marked.Length; i++)
        {
            if (marked[i] != NameMark) { result.Append(marked[i]); continue; }
            result.Append(name);
            if (!hangul || i + 1 >= marked.Length) { continue; }
            char particle = marked[i + 1]; char after = i + 2 < marked.Length ? marked[i + 2] : ' ';
            bool ends = !(after >= '\uAC00' && after <= '\uD7A3');
            char fixedParticle = particle;
            if (ends && (particle == '은' || particle == '는')) { fixedParticle = closed ? '은' : '는'; }
            else if (ends && (particle == '을' || particle == '를')) { fixedParticle = closed ? '을' : '를'; }
            else if (ends && (particle == '과' || particle == '와')) { fixedParticle = closed ? '과' : '와'; }
            else if (ends && (particle == '이' || particle == '가')) { fixedParticle = closed ? '이' : '가'; }
            // "이네", "이다", "이야": 받침이 없으면 '이'가 빠진다.
            else if (particle == '이' && !closed && (after == '네' || after == '다' || after == '야' || after == '라' || after == '지')) { i++; continue; }
            if (fixedParticle != particle) { result.Append(fixedParticle); i++; }
        }
        return result.ToString();
    }
    public static string Clock(ChatHypeLocale locale, int clockMinute)
        => locale.clockFormat.Replace("{h}", (clockMinute / 60).ToString()).Replace("{mm}", (clockMinute % 60).ToString("00")).Replace("{m}", (clockMinute % 60).ToString());
}

/// <summary>이 PC에서 이미 나온 개소리 조합. 같은 조합을 다시 쓰지 않고, 설치마다 다른 순서로 뽑기 위한 값도 여기 둔다.</summary>
public static class ChatHypeMemory
{
    /// <summary>검증·재현 실행에서는 false로 두어 PlayerPrefs를 건드리지 않는다.</summary>
    public static bool Persist = true;
    private const string UsedKey = "DonDon.Hype.Used", CountKey = "DonDon.Hype.Count", SaltKey = "DonDon.Hype.Salt";
    private static HashSet<uint> used;
    private static int count = -1, salt;
    private static void Load()
    {
        if (used != null) { return; }
        used = new HashSet<uint>(); count = 0; salt = 0;
        if (!Persist) { return; }
        count = PlayerPrefs.GetInt(CountKey, 0); salt = PlayerPrefs.GetInt(SaltKey, 0);
        foreach (string part in PlayerPrefs.GetString(UsedKey, "").Split(','))
        { if (uint.TryParse(part, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint value)) { used.Add(value); } }
        if (salt == 0) { salt = Guid.NewGuid().GetHashCode() | 1; PlayerPrefs.SetInt(SaltKey, salt); }
    }
    public static int Salt { get { Load(); return salt; } }
    /// <summary>개소리 봇이 지금까지 입을 연 횟수.</summary>
    public static int Count { get { Load(); return count; } }
    public static bool Used(string combo) { Load(); return used.Contains(CompanyBusiness.Hash(combo)); }
    public static void ForgetCombos() { Load(); used.Clear(); }
    public static void Record(string combo)
    {
        Load(); used.Add(CompanyBusiness.Hash(combo)); count++;
        if (!Persist) { return; }
        var parts = new List<string>(); foreach (uint value in used) { parts.Add(value.ToString("x8")); }
        PlayerPrefs.SetString(UsedKey, string.Join(",", parts)); PlayerPrefs.SetInt(CountKey, count); PlayerPrefs.Save();
    }
    public static void Reset() { used = null; count = -1; }
}

public sealed partial class InvestorSociety
{
    private enum HypePhase { Discover, Flood, Heat, Break }
    private sealed class HypeRun
    {
        public Stock stock;
        public HypePhase phase;
        public double phaseAt, nextLine;
        /// <summary>급등이면 고점, 급락이면 저점.</summary>
        public decimal extreme;
        public int lines;
        /// <summary>군중이 마지막으로 올린 말(맞장구가 여기에 달린다), 이번 폭주에서 한 맞장구의 수, 바로 앞의 말이 맞장구였는가.</summary>
        public long lastId; public int backs; public bool lastWasBack;
        public bool crash, oddballRolled, oddballSpoke;
        public string PhaseKey => crash ? "crash" + phase : phase.ToString().ToLowerInvariant();
        public string GapKey => phase.ToString().ToLowerInvariant();
    }
    private sealed class HypeEcho { public double due; public string kind; public long replyTo; public int minutes; public Stock stock; public bool oddball; }
    private sealed class HypeCall { public Stock stock; public decimal price; public double dueMinute; public bool up; public long messageId; public int minutes; }
    private sealed class HypeCue { public double due; public string when; public Stock stock; public decimal level; }
    private sealed class BreathPiece { public double due; public SocialMessage message; }
    private sealed class PlayerFlow { public Stock stock; public decimal notional; public double at; public bool cued; }

    private readonly Dictionary<Stock, Queue<(double at, decimal price)>> hypeSamples = new();
    private readonly Dictionary<Stock, double> hypeRestUntil = new(), roundRestUntil = new();
    private readonly Dictionary<int, double> hypeSpokeAt = new();
    private readonly Queue<string> hypeRecentLines = new();
    private readonly List<HypeEcho> hypeEchoes = new();
    private readonly List<HypeCall> hypeCalls = new();
    private readonly List<HypeCue> hypeCues = new();
    private readonly List<BreathPiece> breathQueue = new();
    private readonly List<TradeBot> hypeSpeakers = new();
    private readonly List<string> hypeChoices = new();
    private readonly PlayerFlow playerBuys = new(), playerSells = new();
    private HypeRun hypeRun;
    private SocialRandom hypeRandom;
    private TradeBot oddballBot;
    private string oddballTopic, oddballTopicLanguage;
    private bool breathDelivering;
    private int oddballRoster = -1, openCuedDay = -1, closeCuedDay = -1;
    private double hypeNextSample, hypeQuietUntil, hypeTickAt = double.NaN, oddballNextAt = -1, lullSince = -1;
    // 사회 메시지 번호와 겹치지 않는 별도 구간. ChatSocialIds를 쓰지 않으므로 기존 대화의 번호·표현 난수가 밀리지 않는다.
    private long hypeMessageId = 1L << 40;

    private static double Hype(string key) => ChatHypeAssets.Get(key);
    // 배치 검증 실행은 시드로 재현되는 대화를 본다. 거기에 이 연출이 섞이지 않게 기본으로 끈다(DONDON_CHAT_HYPE=1이면 켠다).
    // 정적 초기화에서 Application.isBatchMode를 읽으면 MonoBehaviour 형식 초기화가 예외로 멈춘다. 처음 쓸 때 한 번만 읽는다.
    private static bool hypeAllowedRead, hypeAllowedValue;
    private static bool HypeAllowed
    {
        get
        {
            if (!hypeAllowedRead)
            {
                hypeAllowedValue = !Application.isBatchMode || Environment.GetEnvironmentVariable("DONDON_CHAT_HYPE") == "1";
                hypeAllowedRead = true;
            }

            return hypeAllowedValue;
        }
    }
    private SocialRandom HypeRandom => hypeRandom ??= new SocialRandom(HypeSeed());
    /// <summary>
    /// 평소에는 실행할 때마다 다른 난수열을 쓴다. 검증 실행에서 DONDON_CHAT_SEED에 정수를 주면 그 값으로 고정해
    /// 토론·기분 글·급등락 군중의 말까지 같은 순서로 다시 볼 수 있다.
    /// </summary>
    private static int HypeSeed()
    {
        string wanted = Environment.GetEnvironmentVariable("DONDON_CHAT_SEED");
        if (!string.IsNullOrEmpty(wanted) && int.TryParse(wanted, out int seed)) { return seed; }
        return ChatHypeMemory.Salt ^ unchecked((int)DateTime.UtcNow.Ticks);
    }
    /// <summary>디버그 표시용: 지금 폭주 중인 종목과 단계.</summary>
    public string HypeStatus => hypeRun == null ? "없음" : hypeRun.stock.stockName + " · " + hypeRun.PhaseKey + " · " + hypeRun.lines + "줄";
    public string OddballNickname => ResolveOddball()?.Social.Nickname;

    private void TickHype()
    {
        if (!HypeAllowed || day == null || !SocialReady || !ChatHypeAssets.Ready) { return; }
        double now = SocialTime.Seconds;
        // 불러오기 등으로 시계가 튀면 진행 중이던 연출은 버린다. 치다 만 말은 버리지 않고 마저 내보낸다.
        if (Math.Abs(now - hypeTickAt) > 30) { FlushBreath(double.PositiveInfinity); ResetHype(); }
        hypeTickAt = now;
        FlushBreath(now);
        if (StockManager.Instance == null || Hype("hypeEnabled") <= 0) { return; }
        if (oddballNextAt < 0) { oddballNextAt = now + Hype("oddballWarmupSec"); }
        if (SampleHypePrices(now)) { WatchQuietMoments(now); }
        if (hypeRun == null) { DetectHype(now); } else { AdvanceHype(now); }
        for (int i = hypeCues.Count - 1; i >= 0; i--)
        {
            if (hypeCues[i].due > now) { continue; }
            var cue = hypeCues[i]; hypeCues.RemoveAt(i);
            if (cue.stock.AcceptsOrders && TryOddball(cue.when, cue.stock, now, cue.level) && hypeRun != null && hypeRun.stock == cue.stock) { hypeRun.oddballSpoke = true; }
        }
        for (int i = 0; i < hypeEchoes.Count;)
        {
            if (hypeEchoes[i].due > now) { i++; continue; }
            var echo = hypeEchoes[i]; hypeEchoes.RemoveAt(i); SpeakHypeEcho(echo, now);
        }
        ResolveHypeCalls(now);
    }
    private void ResetHype()
    {
        hypeSamples.Clear(); hypeEchoes.Clear(); hypeCalls.Clear(); hypeCues.Clear(); hypeSpokeAt.Clear(); hypeRecentLines.Clear(); hypeRestUntil.Clear(); roundRestUntil.Clear();
        hypeRun = null; hypeNextSample = 0; hypeQuietUntil = 0; lullSince = -1;
        playerBuys.stock = playerSells.stock = null;
        if (oddballNextAt >= 0) { oddballNextAt = SocialTime.Seconds + Hype("oddballWarmupSec"); }
    }

    // ── 시세 관찰: 누구나 보는 현재가만 쓴다 ──
    private bool SampleHypePrices(double now)
    {
        if (now < hypeNextSample) { return false; }
        hypeNextSample = now + Hype("sampleSec");
        double window = Hype("windowSec");
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null) { continue; }
            if (!stock.IsListed || stock.IsDelisted || stock.currentPrice <= 0m) { hypeSamples.Remove(stock); continue; }
            if (!hypeSamples.TryGetValue(stock, out var samples)) { samples = new Queue<(double, decimal)>(); hypeSamples.Add(stock, samples); }
            decimal before = 0m; foreach (var sample in samples) { before = sample.price; }
            samples.Enqueue((now, stock.currentPrice));
            while (samples.Count > 0 && now - samples.Peek().at > window) { samples.Dequeue(); }
            // 딱 떨어지는 가격을 넘나든 순간.
            decimal level = before > 0m ? RoundLevelCrossed(before, stock.currentPrice) : 0m;
            if (level > 0m && stock.AcceptsOrders && (!roundRestUntil.TryGetValue(stock, out double rest) || now >= rest))
            {
                roundRestUntil[stock] = now + Hype("roundRestSec");
                hypeCues.Add(new HypeCue { due = now + 1 + HypeRandom.Next() * 2, when = "round", stock = stock, level = level });
            }
        }
        return true;
    }
    /// <summary>1,000 이상에서 10ⁿ과 5×10ⁿ. 두 가격 사이에 그런 값이 있으면 돌려준다.</summary>
    private static decimal RoundLevelCrossed(decimal before, decimal after)
    {
        decimal low = Math.Min(before, after), high = Math.Max(before, after);
        for (decimal unit = 1000m; unit <= high; unit *= 10m)
        {
            if (low < unit * 5m && unit * 5m <= high) { return unit * 5m; }
            if (low < unit && unit <= high) { return unit; }
        }
        return 0m;
    }
    private bool HypeWindow(Stock stock, out decimal low, out decimal high, out double span)
    {
        low = high = 0m; span = 0;
        if (stock == null || !hypeSamples.TryGetValue(stock, out var samples) || samples.Count == 0) { return false; }
        double first = double.MaxValue, last = double.MinValue;
        foreach (var sample in samples)
        {
            if (low == 0m || sample.price < low) { low = sample.price; }
            if (sample.price > high) { high = sample.price; }
            first = Math.Min(first, sample.at); last = Math.Max(last, sample.at);
        }
        span = last - first;
        return low > 0m;
    }
    private Stock RandomHypeStock()
    {
        Stock chosen = null; int seen = 0;
        foreach (var stock in hypeSamples.Keys) { if (stock.AcceptsOrders && HypeRandom.Range(0, ++seen) == 0) { chosen = stock; } }
        return chosen;
    }
    /// <summary>극적이지 않은 순간들: 장 시작, 마감 직전, 아무 일도 없는 시간.</summary>
    private void WatchQuietMoments(double now)
    {
        if (day.Phase == MarketPhase.Trading)
        {
            int clock = day.ClockMinute;
            if (openCuedDay != day.Day && clock < 9 * 60 + Hype("openMinutes"))
            { openCuedDay = day.Day; hypeCues.Add(new HypeCue { due = now + 2 + HypeRandom.Next() * 6, when = "open", stock = RandomHypeStock() }); }
            if (closeCuedDay != day.Day && clock >= 17 * 60 - Hype("closeMinutes"))
            { closeCuedDay = day.Day; hypeCues.Add(new HypeCue { due = now + 1 + HypeRandom.Next() * 4, when = "close", stock = RandomHypeStock() }); }
        }
        hypeCues.RemoveAll(cue => cue.stock == null);
        bool quiet = hypeRun == null; double limit = Hype("lullMovePct");
        if (quiet)
        {
            foreach (var stock in hypeSamples.Keys)
            { if (HypeWindow(stock, out decimal low, out decimal high, out _) && (double)(high / low - 1m) * 100d >= limit) { quiet = false; break; } }
        }
        if (!quiet || lullSince < 0) { lullSince = now; return; }
        if (now - lullSince < Hype("lullSec")) { return; }
        lullSince = now;
        var subject = RandomHypeStock();
        if (subject != null) { hypeCues.Add(new HypeCue { due = now, when = "lull", stock = subject }); }
    }
    private void DetectHype(double now)
    {
        Stock best = null; bool crash = false; double bestScore = 1, minimumSpan = Hype("windowSec") * .4;
        foreach (var pair in hypeSamples)
        {
            var stock = pair.Key;
            if (!stock.AcceptsOrders || hypeRestUntil.TryGetValue(stock, out double rest) && now < rest || !HypeWindow(stock, out decimal low, out decimal high, out double span) || span < minimumSpan)
            { continue; }
            double rise = (double)(stock.currentPrice / low - 1m) * 100d / Hype("surgeRisePct"), drop = (double)(1m - stock.currentPrice / high) * 100d / Hype("crashDropPct");
            if (rise < bestScore && drop < bestScore) { continue; }
            // 40초 사이의 움직임만으로 방 전체가 들끓지는 않는다. 하루 전체에서 그 움직임이 무엇인지 본다:
            // +500%였던 종목이 12% 내려온 것은 눌림이지 폭락이 아니고(차트 수다가 조용히 다룬다), −35%인 종목이 12% 튄 것은 급등이 아니다.
            var read = SituationOf(stock);
            bool crashFits = !read.valid || ChatSituationRules.CrashRunFits(read.kind), surgeFits = !read.valid || ChatSituationRules.SurgeRunFits(read.kind);
            if (rise >= bestScore && surgeFits) { best = stock; bestScore = rise; crash = false; }
            if (drop >= bestScore && crashFits) { best = stock; bestScore = drop; crash = true; }
        }
        if (best != null) { StartHype(best, crash, now); }
    }
    private void StartHype(Stock stock, bool crash, double now)
    {
        hypeRun = new HypeRun { stock = stock, crash = crash, phase = HypePhase.Discover, phaseAt = now, nextLine = now + .3, extreme = stock.currentPrice };
        hypeRecentLines.Clear();
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "HYPE_START", reason = stock.symbol + (crash ? " crash" : " surge"), value = (double)stock.currentPrice });
    }
    private void EndHype(double now, string reason)
    {
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "HYPE_END", reason = hypeRun.stock.symbol + " " + reason, value = hypeRun.lines });
        hypeRestUntil[hypeRun.stock] = now + Hype("hypeCooldownSec"); hypeRun = null;
    }
    private void EnterHypePhase(HypePhase phase, double now)
    {
        hypeRun.phase = phase; hypeRun.phaseAt = now;
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "HYPE_PHASE", reason = hypeRun.stock.symbol + " " + hypeRun.PhaseKey, value = hypeRun.lines });
    }
    private void AdvanceHype(double now)
    {
        var run = hypeRun; var stock = run.stock; decimal price = stock.currentPrice;
        if (!stock.AcceptsOrders || price <= 0m) { EndHype(now, "halted"); return; }
        if (run.crash ? price < run.extreme : price > run.extreme) { run.extreme = price; }
        decimal turn = (decimal)Hype("breakDropPct") / 100m;
        if (run.phase != HypePhase.Break && (run.crash ? price >= run.extreme * (1m + turn) : price <= run.extreme * (1m - turn)))
        {
            // 방향이 꺾이면 방이 먼저 조용해진다. 급등이 꺾였을 때 개소리 봇이 낀다면 그 정적 한가운데다.
            EnterHypePhase(HypePhase.Break, now);
            double quiet = Hype("breakQuietSec");
            hypeQuietUntil = Math.Max(hypeQuietUntil, now + quiet); run.nextLine = now + quiet;
            if (!run.crash && !run.oddballSpoke) { hypeCues.Add(new HypeCue { due = now + quiet * .6, when = "break", stock = stock }); }
            return;
        }
        double age = now - run.phaseAt;
        if (run.phase == HypePhase.Discover && age >= Hype("discoverSec")) { EnterHypePhase(HypePhase.Flood, now); }
        else if (run.phase == HypePhase.Flood && age >= Hype("floodSec")) { EnterHypePhase(HypePhase.Heat, now); }
        else if (run.phase == HypePhase.Heat && age >= Hype("heatSec")) { EndHype(now, "faded"); return; }
        else if (run.phase == HypePhase.Break && age >= Hype("breakSec")) { EndHype(now, "turned"); return; }
        if (now < run.nextLine || now < hypeQuietUntil) { return; }
        if (run.phase == HypePhase.Flood && !run.oddballRolled && run.lines >= Hype("oddballFloodLines"))
        {
            // 다들 짧게 소리칠 때 혼자 차분하게. 굴림은 폭주 한 번에 한 번뿐이다.
            run.oddballRolled = true;
            if (TryOddball(run.crash ? "crash" : "surge", stock, now)) { run.oddballSpoke = true; run.nextLine = hypeQuietUntil; return; }
        }
        if (SpeakHypeCrowd(run, now)) { run.lines++; }
        run.nextLine = now + Hype(run.GapKey + "GapMin") + HypeRandom.Next() * (Hype(run.GapKey + "GapMax") - Hype(run.GapKey + "GapMin"));
    }

    // ── 군중 ──
    private TradeBot PickHypeSpeaker(double now)
    {
        hypeSpeakers.Clear();
        var odd = ResolveOddball(); double cooldown = Hype("crowdBotCooldownSec");
        foreach (var bot in socialBots)
        {
            if (bot == odd || !bot.isActiveAndEnabled || !Present(bot, 0) || !bot.Social.Psychology.ActiveAt((float)now) || IsTyping(bot) ||
                hypeSpokeAt.TryGetValue(bot.Social.Id, out double spoke) && now - spoke < cooldown)
            { continue; }
            hypeSpeakers.Add(bot);
        }
        return hypeSpeakers.Count == 0 ? null : hypeSpeakers[HypeRandom.Range(0, hypeSpeakers.Count)];
    }
    private string PickHypeText(string[] common, string[] own)
    {
        hypeChoices.Clear();
        foreach (string text in common) { if (!hypeRecentLines.Contains(text)) { hypeChoices.Add(text); } }
        foreach (string text in own) { if (!hypeRecentLines.Contains(text)) { hypeChoices.Add(text); } }
        if (hypeChoices.Count == 0) { hypeChoices.AddRange(common); hypeChoices.AddRange(own); }
        if (hypeChoices.Count == 0) { return null; }
        string chosen = hypeChoices[HypeRandom.Range(0, hypeChoices.Count)];
        hypeRecentLines.Enqueue(chosen);
        while (hypeRecentLines.Count > Hype("crowdRepeatWindow")) { hypeRecentLines.Dequeue(); }
        return chosen;
    }
    private readonly List<string> hypeOwn = new();
    private bool SpeakHypeCrowd(HypeRun run, double now)
    {
        var bot = PickHypeSpeaker(now);
        if (bot == null) { return false; }
        // 들고 있는 봇만 보유자 대사를 한다. 없는 봇이 "탔다"고 하지 않는다.
        bool held = bot.GetOwnedVolume(run.stock) > 0m;
        bool formal = bot.Social.Psychology.Traits.Politeness >= Config.Int("styleFormalMin");
        string text = null;
        // 말맛(ChatVoice.cs): 세기는 폭주의 단계가 정한다. 알아채는 동안 2, 쏟아질 때 3, 식어 갈 때 2, 꺾인 뒤 1.
        var voice = formal ? null : ChatReactionAssets.Voice(ChatHypeAssets.LanguageCode);
        int level = run.phase == HypePhase.Flood ? ChatVoice.Extreme : run.phase == HypePhase.Break ? ChatVoice.Warm : ChatVoice.Strong;
        var lean = run.phase == HypePhase.Break ? ChatVoiceLean.Calm : run.crash ? ChatVoiceLean.Down : ChatVoiceLean.Up;
        // 다들 한마디씩 쏟아낼 때 누군가는 앞사람 말에 "ㄹㅇ", "ㅋㅋㅋㅋ" 하고 맞장구만 친다. 연달아 하지 않고, 폭주 한 번에 몇 번만 한다.
        if (voice != null && run.lastId != 0 && !run.lastWasBack && run.backs < voice.backMaxPerRun && (run.phase == HypePhase.Flood || run.phase == HypePhase.Heat) &&
            BackDue(voice, now) && HypeRandom.Next() < voice.backChance)
        {
            string back = TakeBack(voice, ChatVoice.BackMood(lean, HypeRandom.Next()), level, HypeRandom.Next, now);
            if (back != null)
            {
                run.backs++; run.lastWasBack = true;
                PublishHype(bot, back, "hype." + run.PhaseKey + ".back", run.stock, run.lastId, 0);
                hypeSpokeAt[bot.Social.Id] = now;
                return true;
            }
        }
        bool dressed = false;
        if (!formal)
        {
            // 같은 급등(급락)을 보고도 사람마다 하는 말이 다르다: 물려 있던 사람은 본전을 말하고, 수익 중인 사람은 느긋하고,
            // 욕심 많은 사람은 더 사려 하고, 겁 많은 사람은 꺼질까 걱정하고, 허세 센 사람은 "내가 뭐랬냐" 한다.
            // 처지는 실제 보유와 평단에서, 기질은 그 봇의 욕심·공포·허세에서 읽는다. 맞는 대사가 있으면 절반 넘게 그쪽에서 고른다.
            hypeOwn.Clear();
            var mind = bot.Social.Psychology;
            if (held)
            {
                decimal basis = bot.GetOwnedAverage(run.stock);
                if (basis > 0m) { hypeOwn.AddRange(ChatHypeAssets.CrowdOf(run.PhaseKey, run.stock.currentPrice >= basis ? "heldGain" : "heldLoss", null)); }
            }
            string tone = mind.Traits.Bravado >= 72 ? "cocky" : mind.Fear >= 40f && mind.Fear >= mind.Greed * .6f ? "fearful" : mind.Greed >= 72f ? "greedy" : null;
            if (tone != null)
            {
                hypeOwn.AddRange(ChatHypeAssets.CrowdOf(run.PhaseKey, "any", tone));
                hypeOwn.AddRange(ChatHypeAssets.CrowdOf(run.PhaseKey, held ? "held" : "notHeld", tone));
            }
            if (hypeOwn.Count > 0 && HypeRandom.Next() < .6) { text = PickHypeText(Array.Empty<string>(), hypeOwn.ToArray()); }
        }
        // 급락 군중의 보유자 대사는 물린 사람의 말이다("나 물림", "손절각인가"). 수익 중인 보유자는 그 대사를 하지 않는다:
        // 크게 수익 중이면 그 사람의 처지에 맞춘 상황 대사를 하고("많이 빠지긴 하네. 그래도 수익"), 아니면 누구나 할 수 있는 말만 한다.
        bool gaining = false;
        if (held && run.crash && !formal)
        {
            var view = BotView(bot, run.stock, out _);
            gaining = view == ChatSpeakerView.DeepProfit || view == ChatSpeakerView.Profit;
            // 상황 대사는 엔진이 이미 말맛을 입혀서 준다.
            if (text == null && view == ChatSpeakerView.DeepProfit && HypeRandom.Next() < .6) { text = SituationLine(bot, run.stock, SituationOf(run.stock), out _); dressed = text != null; }
        }
        text ??= PickHypeText(ChatHypeAssets.Crowd(run.PhaseKey, "any", formal), gaining ? Array.Empty<string>() : ChatHypeAssets.Crowd(run.PhaseKey, held ? "held" : "notHeld", formal));
        if (text == null) { return false; }
        if (voice != null && !dressed) { text = ChatVoice.Apply(text, voice, level, lean, SpeechHabit(bot.Social.Id), HypeRandom.Next); }
        // 쏟아지는 구간의 짧은 말은 그대로 두고, 말이 길어지는 구간에서만 그 봇의 습관대로 끊는다.
        var said = PublishHype(bot, ChatHypeAssets.Fill(text, HypeSlots(run.stock, 0, 0m)), "hype." + run.PhaseKey, run.stock, 0, run.phase == HypePhase.Flood ? 0 : 1);
        run.lastId = said.last.id; run.lastWasBack = false;
        hypeSpokeAt[bot.Social.Id] = now;
        return true;
    }
    private void SpeakHypeEcho(HypeEcho echo, double now)
    {
        var bot = echo.oddball ? ResolveOddball() : PickHypeSpeaker(now);
        if (bot == null || !Present(bot, 0)) { return; }
        string text = PickHypeText(ChatHypeAssets.Echo(echo.kind), Array.Empty<string>());
        if (text == null) { return; }
        PublishHype(bot, ChatHypeAssets.Fill(text, HypeSlots(echo.stock, echo.minutes, 0m)), "hype.echo." + echo.kind, echo.stock, echo.replyTo, 0);
        hypeSpokeAt[bot.Social.Id] = now;
    }
    private Dictionary<string, string> HypeSlots(Stock stock, int minutes, decimal level)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal) { ["minutes"] = minutes.ToString(SocialCulture), ["count"] = (hypeRun?.lines ?? 0).ToString(SocialCulture) };
        slots["clock"] = ChatHypeAssets.Clock(ChatHypeAssets.Current, day.ClockMinute);
        slots["round"] = Money(level);
        if (stock == null) { return slots; }
        // 종목 이름은 그 언어에서 보이는 이름 그대로 쓴다. 글자 수도 그 이름으로 센다.
        string name = GameLocale.Text(stock.stockName ?? stock.symbol ?? "");
        slots["stockName"] = name; slots["nameLen"] = name.Replace(" ", "").Length.ToString(SocialCulture);
        // 줄임말(산삼전자 → 산전). 쓸지 말지는 빈칸을 채울 때 정한다.
        string shortName = ChatHypeAssets.ShortName?.Invoke(stock.symbol);
        if (!string.IsNullOrEmpty(shortName)) { slots["stockShort"] = shortName; }
        slots["price"] = Money(stock.currentPrice);
        long whole = (long)stock.currentPrice;
        while (whole >= 10 && whole % 10 == 0) { whole /= 10; }
        slots["digit"] = (whole % 10).ToString(SocialCulture);
        decimal move = 0m;
        if (HypeWindow(stock, out decimal low, out decimal high, out _))
        {
            decimal rise = (stock.currentPrice / low - 1m) * 100m, drop = (stock.currentPrice / high - 1m) * 100m;
            move = hypeRun != null && hypeRun.stock == stock ? (hypeRun.crash ? drop : rise) : rise >= -drop ? rise : drop;
        }
        slots["movePct"] = Percent(move);
        // 하루 전체에서 본 수치(전일 대비, 고점·저점 대비, 오른 폭 가운데 돌려준 몫 …). 급등락 군중·차트 수다·잔움직임·토론·기분 글이 모두 같은 꼴로 받는다.
        var read = SituationOf(stock);
        slots["dayPct"] = ChatSituationMath.SignedPercent(read.valid ? read.dayPct : 0m);
        ChatReactionAssets.Current?.FillSlots(read, slots);
        return slots;
    }
    /// <summary>
    /// 표시만 한다. room.recent·목격·감정·스레드·기억 어디에도 넣지 않는다.
    /// breath: 0 한 번에, 1 그 봇의 습관대로, 2 문장마다(개소리 봇). 마지막 조각과 그 조각이 뜨는 시각을 돌려준다.
    /// </summary>
    private (SocialMessage last, double doneAt) PublishHype(TradeBot bot, string text, string key, Stock stock, long replyTo, int breath)
    {
        var message = new SocialMessage { id = ++hypeMessageId, roomId = 0, senderId = bot.Social.Psychology.SocialId, at = SocialTime.GameMinute, realAt = SocialTime.Seconds,
            act = SocialAct.REACT, replyToId = replyTo, topicEvent = "HYPE", templateKey = key, text = text,
            facts = new SocialFacts { symbol = stock?.symbol, stockName = stock?.stockName, at = SocialTime.GameMinute },
            debug = new SocialMessageDebug { trigger = "HYPE" } };
        // 방이 지금 어느 종목으로 떠드는지는 알려 둔다. 그래야 뒤이어 말을 꺼내는 봇들이 같은 종목을 화제로 삼는다
        // (예전에는 군중이 A 종목 급등으로 들끓는 동안 대화 쪽 봇들은 그걸 모르고 딴 종목 얘기를 꺼냈다).
        if (stock != null) { ObserveConversationTopic(message); }
        List<string> pieces = null;
        var rules = ChatHypeAssets.Current.breath;
        if (breath == 2) { pieces = ChatBreath.Split(text, rules, (int)Hype("oddballMaxPieces"), ChatBreathMode.Sentences); }
        else if (breath == 1 && Hype("breathEnabled") > 0 && BreathMode(bot, message.id, out var mode)) { pieces = ChatBreath.Split(text, rules, (int)Hype("breathMaxPieces"), mode); }
        if (pieces != null) { return (message, QueueBreath(message, pieces, rules)); }
        breathDelivering = true;
        try { DeliverSocialMessage(message); } finally { breathDelivering = false; }
        return (message, SocialTime.Seconds);
    }

    // ── 끊어 치기 ──
    /// <summary>끊어 치는 습관은 봇마다 정해져 있다. 많이 끊는 봇, 가끔 끊는 봇, 안 끊는 봇. 봇의 난수열은 쓰지 않는다.</summary>
    private static bool BreathMode(TradeBot bot, long messageId, out ChatBreathMode mode)
    {
        mode = ChatBreathMode.Light;
        double habit = CompanyBusiness.Hash("BREATH:" + bot.Social.Id) % 1000 / 1000d, roll = CompanyBusiness.Hash("BREATHMSG:" + bot.Social.Id + ":" + messageId) % 1000 / 1000d;
        double heavy = Hype("breathHeavyShare");
        if (habit < heavy) { mode = ChatBreathMode.Heavy; return roll < Hype("breathHeavyChance"); }
        return habit < heavy + Hype("breathLightShare") && roll < Hype("breathLightChance");
    }
    /// <summary>기존 대화가 화면으로 나가는 길목. 끊어 칠 말이면 조각으로 나눠 내보내고 true를 돌려준다.</summary>
    private bool SplitSocialDelivery(TradeBot bot, SocialMessage message)
    {
        if (breathDelivering || !HypeAllowed || day == null || message == null || string.IsNullOrEmpty(message.text) || message.facts != null && message.facts.signal ||
            !ChatHypeAssets.Ready || Hype("breathEnabled") <= 0 || !BreathMode(bot, message.id, out var mode))
        { return false; }
        // 화면에 보일 언어의 문장을 그 언어의 규칙으로 끊는다.
        string code = ChatHypeAssets.LanguageCode;
        if (code != GameLocale.Code) { return false; }
        var rules = ChatHypeAssets.Current.breath;
        var pieces = ChatBreath.Split(LocalizedChat.Text(message, message.text), rules, (int)Hype("breathMaxPieces"), mode);
        if (pieces == null) { return false; }
        QueueBreath(message, pieces, rules);
        return true;
    }
    private double QueueBreath(SocialMessage source, List<string> pieces, ChatBreathRules rules)
    {
        double due = SocialTime.Seconds;
        for (int i = 0; i < pieces.Count; i++)
        {
            bool last = i == pieces.Count - 1;
            // 조각은 이미 그 언어로 확정된 글이다. 마지막 조각이 원래 메시지의 번호와 인증 카드를 잇는다.
            var piece = new SocialMessage { id = last ? source.id : ++hypeMessageId, replyToId = source.replyToId, threadId = source.threadId, roomId = source.roomId, senderId = source.senderId,
                targetId = source.targetId, at = source.at, realAt = source.realAt, act = source.act, facts = source.facts, topicEvent = source.topicEvent, templateKey = source.templateKey,
                text = pieces[i], isLie = source.isLie, memory = source.memory, promisedAction = source.promisedAction, debug = source.debug, profitCard = last ? source.profitCard : null };
            if (i > 0) { due += ChatBreath.Gap(pieces[i], rules); }
            breathQueue.Add(new BreathPiece { due = due, message = piece });
        }
        FlushBreath(SocialTime.Seconds);
        return due;
    }
    private void FlushBreath(double now)
    {
        if (breathQueue.Count == 0 || breathDelivering) { return; }
        breathDelivering = true;
        try
        {
            for (int i = 0; i < breathQueue.Count;)
            {
                if (breathQueue[i].due > now) { i++; continue; }
                var piece = breathQueue[i]; breathQueue.RemoveAt(i);
                DeliverSocialMessage(piece.message);
            }
        }
        finally { breathDelivering = false; }
    }

    // ── 개소리 봇 ──
    private TradeBot ResolveOddball()
    {
        if (oddballRoster == socialBots.Count) { return oddballBot; }
        oddballRoster = socialBots.Count; oddballBot = null;
        var leader = NpcLeadingRoom.Instance == null ? null : NpcLeadingRoom.Instance.Leader;
        uint best = 0;
        // 명단이 같으면 늘 같은 한 명. "그 사람"으로 기억되게 한다.
        foreach (var bot in socialBots)
        {
            if (bot == leader) { continue; }
            uint hash = CompanyBusiness.Hash("ODDBALL:" + bot.Social.Id);
            if (oddballBot == null || hash < best) { best = hash; oddballBot = bot; }
        }
        return oddballBot;
    }
    private static string OddballChanceKey(string when) => when switch
    {
        "surge" => "oddballSurgeChance", "break" => "oddballBreakChance", "crash" => "oddballCrashChance", "round" => "oddballRoundChance",
        "lull" => "oddballLullChance", "open" => "oddballOpenChance", "close" => "oddballCloseChance", _ => "oddballPlayerChance"
    };
    private bool TryOddball(string when, Stock stock, double now, decimal level = 0m, bool force = false)
    {
        var bot = ResolveOddball();
        if (bot == null || stock == null || !bot.isActiveAndEnabled || !Present(bot, 0)) { return false; }
        if (!force && (Hype("oddballEnabled") <= 0 || now < oddballNextAt || HypeRandom.Next() >= Hype(OddballChanceKey(when)))) { return false; }
        string code = ChatHypeAssets.LanguageCode; var locale = ChatHypeAssets.Current;
        var fresh = new List<(ChatHypeSetup setup, ChatHypeVerdict verdict)>(); var all = new List<(ChatHypeSetup setup, ChatHypeVerdict verdict)>();
        foreach (var setup in locale.setups)
        {
            if (setup.when != when || hypeRun == null && setup.text.Contains("{count}")) { continue; }
            if (setup.solo) { Consider(setup, null); continue; }
            foreach (var verdict in locale.verdicts) { if (ChatHypeAssets.Fits(setup, verdict)) { Consider(setup, verdict); } }
        }
        if (all.Count == 0) { return false; }
        if (fresh.Count == 0) { ChatHypeMemory.ForgetCombos(); fresh = all; }
        var pick = fresh[HypeRandom.Range(0, fresh.Count)];
        string combo = Combo(pick.setup, pick.verdict);
        int minutes = HypeRandom.Range((int)Hype("predictMinMinutes"), (int)Hype("predictMaxMinutes") + 1);
        bool repeatVisitor = ChatHypeMemory.Count > 0;
        var slots = HypeSlots(stock, minutes, level);
        string text = pick.verdict == null ? pick.setup.text : pick.setup.text + " " + pick.verdict.text;
        // 가끔 지난번에 한 말부터 되짚고 시작한다. 같은 언어로 했던 말만 되짚는다.
        if (!string.IsNullOrEmpty(oddballTopic) && oddballTopicLanguage == code && locale.callbacks != null && locale.callbacks.Length > 0 && HypeRandom.Next() < Hype("oddballCallbackChance"))
        {
            slots["topic"] = oddballTopic;
            text = locale.callbacks[HypeRandom.Range(0, locale.callbacks.Length)] + " " + text;
        }
        oddballTopic = pick.setup.topic; oddballTopicLanguage = code;
        // 개소리 봇은 문장마다 엔터를 친다. 한 줄씩 떨어져야 박자가 산다.
        var said = PublishHype(bot, ChatHypeAssets.Fill(text, slots), "oddball." + combo, stock, 0, 2);
        ChatHypeMemory.Record(code + ":" + combo);
        oddballNextAt = now + Hype("oddballCooldownSec");
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "ODDBALL", id = said.last.id, actor = bot.Social.Id, reason = when + " " + stock.symbol + " " + code + " " + combo });
        if (pick.verdict != null && pick.verdict.predict)
        {
            hypeCalls.Add(new HypeCall { stock = stock, price = stock.currentPrice, dueMinute = day.AbsoluteGameMinute + minutes, up = pick.verdict.lean == "up", messageId = said.last.id, minutes = minutes });
        }
        // 마지막 줄이 뜨고 한 박자 정적 뒤에 방이 반응한다.
        double hush = Hype("oddballHushSec");
        hypeQuietUntil = Math.Max(hypeQuietUntil, said.doneAt + hush);
        double first = said.doneAt + hush + HypeRandom.Next();
        hypeEchoes.Add(new HypeEcho { due = first, kind = repeatVisitor && HypeRandom.Next() < .6 ? "again" : "first", replyTo = said.last.id, stock = stock });
        if (HypeRandom.Next() < Hype("oddballSecondEchoChance"))
        { hypeEchoes.Add(new HypeEcho { due = first + 1.2 + HypeRandom.Next() * 1.3, kind = "first", replyTo = said.last.id, stock = stock }); }
        return true;

        string Combo(ChatHypeSetup setup, ChatHypeVerdict verdict) => setup.key + "|" + (verdict == null ? "-" : verdict.key);
        void Consider(ChatHypeSetup setup, ChatHypeVerdict verdict)
        {
            all.Add((setup, verdict));
            if (!ChatHypeMemory.Used(code + ":" + Combo(setup, verdict))) { fresh.Add((setup, verdict)); }
        }
    }
    private void ResolveHypeCalls(double now)
    {
        double minute = day.AbsoluteGameMinute;
        for (int i = hypeCalls.Count - 1; i >= 0; i--)
        {
            var call = hypeCalls[i];
            if (minute < call.dueMinute) { continue; }
            hypeCalls.RemoveAt(i);
            // 장이 끝나 시간이 건너뛰었거나 종목이 사라졌으면 조용히 잊는다.
            if (minute - call.dueMinute > 30 || !call.stock.IsListed || call.stock.IsDelisted || call.stock.currentPrice <= 0m) { continue; }
            bool right = call.up ? call.stock.currentPrice > call.price : call.stock.currentPrice < call.price;
            ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = right ? "ODDBALL_CALL_RIGHT" : "ODDBALL_CALL_WRONG", id = call.messageId,
                reason = call.stock.symbol + " " + Money(call.price) + " -> " + Money(call.stock.currentPrice), value = call.minutes });
            if (right)
            {
                // 맞으면 한마디만 한다. 틀리면 본인은 아무 말도 하지 않는다.
                hypeEchoes.Add(new HypeEcho { due = now, kind = "gloat", replyTo = call.messageId, minutes = call.minutes, stock = call.stock, oddball = true });
                hypeEchoes.Add(new HypeEcho { due = now + 2 + HypeRandom.Next() * 1.5, kind = "right", replyTo = call.messageId, minutes = call.minutes, stock = call.stock });
            }
            else if (HypeRandom.Next() < Hype("predictWrongCallChance"))
            { hypeEchoes.Add(new HypeEcho { due = now + HypeRandom.Next() * 2, kind = "wrong", replyTo = call.messageId, minutes = call.minutes, stock = call.stock }); }
        }
    }

    // ── 플레이어의 큰 체결 ──
    // 체결 크기는 누구나 보는 정보다. 대사는 "방금 산 분", "방금 판 분"이라고만 하고 그게 누구인지는 말하지 않는다.
    private void ObserveHypeFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller)
    {
        var accounts = PlayerAccounts.Instance;
        if (!HypeAllowed || accounts == null || stock == null || !ChatHypeAssets.Ready || Hype("hypeEnabled") <= 0) { return; }
        bool bought = ReferenceEquals(buyer, accounts.primary) && !PlayerAccounts.IsPlayer(seller), sold = ReferenceEquals(seller, accounts.primary) && !PlayerAccounts.IsPlayer(buyer);
        if (!bought && !sold) { return; }
        double now = SessionClock.Time;
        var flow = bought ? playerBuys : playerSells;
        if (flow.stock != stock || now - flow.at > Hype("playerWindowSec")) { flow.stock = stock; flow.notional = 0m; flow.cued = false; }
        flow.at = now; flow.notional += price * volume;
        decimal equity = accounts.primary.Equity();
        if (flow.cued || equity <= 0m || flow.notional < equity * (decimal)Hype("playerBigShare") || !HypeWindow(stock, out decimal low, out decimal high, out _)) { return; }
        string when = "playerSell";
        if (bought)
        {
            double rise = (double)(price / low - 1m) * 100d, drop = (double)(1m - price / high) * 100d;
            when = drop >= Hype("playerDipDropPct") ? "playerDip" : rise >= Hype("playerChaseRisePct") ? "playerBuy" : null;
            if (when == null) { return; }
        }
        flow.cued = true;
        double delay = Hype("playerDelayMinSec") + HypeRandom.Next() * (Hype("playerDelayMaxSec") - Hype("playerDelayMinSec"));
        hypeCues.Add(new HypeCue { due = now + delay, when = when, stock = stock });
    }

    // ── 에디터 확인용 ──
    public bool ForceHype(bool crash)
    {
        if (day == null || !SocialReady || !ChatHypeAssets.Ready || StockManager.Instance == null) { return false; }
        Stock best = null; decimal bestMove = decimal.MinValue;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || !stock.AcceptsOrders || stock.currentPrice <= 0m) { continue; }
            decimal move = !HypeWindow(stock, out decimal low, out decimal high, out _) ? 0m : crash ? high / stock.currentPrice : stock.currentPrice / low;
            if (best == null || move > bestMove) { best = stock; bestMove = move; }
        }
        if (best == null) { return false; }
        if (hypeRun != null) { EndHype(SocialTime.Seconds, "forced"); }
        StartHype(best, crash, SocialTime.Seconds);
        return true;
    }
    public bool ForceOddball(string when)
    {
        if (day == null || !SocialReady || !ChatHypeAssets.Ready || StockManager.Instance == null) { return false; }
        var stock = hypeRun?.stock;
        if (stock == null) { foreach (var candidate in StockManager.Instance.stocks) { if (candidate != null && candidate.AcceptsOrders && candidate.currentPrice > 0m) { stock = candidate; break; } } }
        decimal level = 0m;
        if (when == "round" && stock != null) { level = 1000m; while (level * 10m <= stock.currentPrice) { level *= 10m; } }
        return TryOddball(when, stock, SocialTime.Seconds, level, true);
    }
}
