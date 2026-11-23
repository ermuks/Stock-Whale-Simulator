using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

/// <summary>Presentation-only bridge to Unity Localization. Simulation strings and RNG remain unchanged.</summary>
[DefaultExecutionOrder(-20000)]
public sealed class GameLocale : MonoBehaviour
{
    [Serializable] public sealed class SourceEntry { public string key, source; public bool templateOnly; }
    [Serializable] public sealed class SourceCatalog { public SourceEntry[] entries; }
    private sealed class Phrase
    {
        public string source, translated;
        public Regex pattern;
        public string hint;
        public string[] slots;
    }
    private sealed class Trie
    {
        public readonly Dictionary<char,Trie> children = new();
        public Phrase phrase;
    }
    public const string TableName = "DonDon";
    private const string Preference = "DonDon.Language";
    private static readonly Regex Token = new(@"\{([^{}:,]+)(?:[^{}]*)\}", RegexOptions.CultureInvariant);
    private static readonly Regex Markup = new(@"<[^>]*>", RegexOptions.CultureInvariant);
    private static readonly Regex Segments = new(@"(<noparse>[\s\S]*?</noparse>|<[^>]*>|\r\n|\n|\r)", RegexOptions.CultureInvariant);
    private static readonly Dictionary<string,string> rendered = new(StringComparer.Ordinal);
    private static readonly Dictionary<string,Phrase> exact = new(StringComparer.Ordinal);
    private static readonly List<Phrase> patterns = new();
    private static readonly HashSet<string> literals = new(StringComparer.Ordinal);
    private static readonly Dictionary<char,List<string>> literalSpans = new();
    private static Trie trie = new();
    private static GameLocale instance;
    private static SourceCatalog catalog;
    private static StringTable table;
    private static int loadTicket;
    public static string Code { get; private set; } = "ko";
    public static int Revision { get; private set; }
    public static bool Ready { get; private set; }
    public static event Action Changed;
    public static readonly HashSet<string> MissingSources = new(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        instance = null; catalog = null; table = null; Code = "ko"; Revision = 0; Ready = false; loadTicket = 0;
        exact.Clear(); patterns.Clear(); rendered.Clear(); literals.Clear(); literalSpans.Clear(); MissingSources.Clear(); trie = new(); Changed = null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (instance != null) { return; }
        var root = new GameObject("System :: Localization");
        DontDestroyOnLoad(root); instance = root.AddComponent<GameLocale>();
    }
    private IEnumerator Start()
    {
        var data = Resources.Load<TextAsset>("Localization/SourceIndex");
        if (data == null) { Debug.LogError("Localization source index is missing."); yield break; }
        catalog = JsonUtility.FromJson<SourceCatalog>(data.text);
        GameLocaleFonts.Load();
        var initialization = LocalizationSettings.InitializationOperation;
        yield return initialization;
        if (initialization.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        { Debug.LogError("Localization initialization failed: " + initialization.OperationException); yield break; }
        LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        SceneManager.sceneLoaded += SceneLoaded;
        string saved = PlayerPrefs.GetString(Preference,"ko");
        if (!Supported(saved)) { saved = "ko"; }
        var selected = LocalizationSettings.AvailableLocales.GetLocale(saved);
        if (selected == null) { Debug.LogError("Missing supported locale: " + saved); yield break; }
        LocalizationSettings.SelectedLocale = selected;
        yield return Load(selected, ++loadTicket);
        ScanScene(SceneManager.GetActiveScene());
    }
    public static bool Supported(string code) => code == "ko" || code == "en" || code == "ja";
    public static void SetLanguage(string code)
    {
        if (!Supported(code)) { throw new ArgumentOutOfRangeException(nameof(code)); }
        PlayerPrefs.SetString(Preference,code); PlayerPrefs.Save();
        if (!LocalizationSettings.InitializationOperation.IsDone) { return; }
        var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
        if (locale == null) { throw new InvalidOperationException("Supported locale asset missing: " + code); }
        LocalizationSettings.SelectedLocale = locale;
    }
    private void LocaleChanged(Locale locale)
    {
        if (!Supported(locale.Identifier.Code)) { SetLanguage("ko"); return; }
        StartCoroutine(Load(locale,++loadTicket));
    }
    private IEnumerator Load(Locale locale, int ticket)
    {
        var operation = LocalizationSettings.StringDatabase.GetTableAsync(TableName,locale);
        yield return operation;
        if (ticket != loadTicket) { yield break; }
        if (operation.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded || operation.Result == null)
        { Debug.LogError("Could not load localization table: " + locale.Identifier.Code); yield break; }
        table = operation.Result; Code = locale.Identifier.Code;
        exact.Clear(); patterns.Clear(); rendered.Clear(); trie = new(); MissingSources.Clear();
        foreach (var row in catalog.entries)
        {
            var entry = table.GetEntry(row.key);
            if (entry == null || string.IsNullOrEmpty(entry.Value)) { MissingSources.Add(row.source); continue; }
            var phrase = new Phrase { source = row.source, translated = entry.Value };
            exact[row.source] = phrase;
            var slots = Token.Matches(row.source);
            if (slots.Count > 0)
            {
                if (row.templateOnly) { continue; }
                var expression = new StringBuilder("\\A"); int cursor = 0;
                var names = new List<string>(); string hint = "";
                foreach (Match slot in slots)
                {
                    string literal = row.source.Substring(cursor,slot.Index-cursor);
                    if (literal.Length > hint.Length) { hint = literal; }
                    expression.Append(Regex.Escape(literal)).Append("(?<v").Append(names.Count).Append(">.*?)");
                    names.Add(slot.Groups[1].Value); cursor = slot.Index + slot.Length;
                }
                string tail = row.source.Substring(cursor); if (tail.Length > hint.Length) { hint = tail; }
                expression.Append(Regex.Escape(tail)).Append("\\z");
                if (hint.Length >= 2)
                {
                    phrase.pattern = new Regex(expression.ToString(),RegexOptions.Singleline | RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(8));
                    phrase.hint = hint; phrase.slots = names.ToArray(); patterns.Add(phrase);
                }
                continue;
            }
            if (row.source.Length < 2 || !HasKorean(row.source)) { continue; }
            var node = trie;
            foreach (char c in row.source)
            {
                if (!node.children.TryGetValue(c,out var child)) { node.children[c] = child = new Trie(); }
                node = child;
            }
            node.phrase = phrase;
        }
        patterns.Sort((a,b) => b.source.Length.CompareTo(a.source.Length));
        Ready = true; Revision++;
        PlayerPrefs.SetString(Preference,Code);
        Changed?.Invoke();
    }
    public static string Text(string source) => Render(source,0);
    /// <summary>게임 날짜. 한국어 "3일", 영어 "Day 3", 일본어 "3日目".</summary>
    /// <summary>장날의 번호를 달력 날짜로 바꿔 적는다(여섯째 장날 → "8일"). 이미 달력 날짜인 값은 DateText로 적는다.</summary>
    public static string Day(int day) => DateText(MarketDayController.DateOf(day));
    public static string DateText(int date) => Code == "ja" ? date + "日目" : Code == "en" ? "Day " + date : date + "일";
    // 저장된 시각 표기("3일 09:02")는 한국어로 적혀 있다. 다른 언어에서는 문장 안에 있어도 그 언어의 표기로 바꾼다.
    private static readonly Regex DayStamp = new(@"(?<![\d가-힣])(?<d>\d+)일 (?<t>\d{1,2}:\d{2})", RegexOptions.Compiled);
    public static string Get(string key,string fallback) => table?.GetEntry(key)?.Value ?? fallback;
    public static void ProtectLiteral(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && literals.Add(value))
        {
            if (!literalSpans.TryGetValue(value[0],out var bucket)) { literalSpans[value[0]] = bucket = new(); }
            bucket.Add(value); bucket.Sort((a,b) => b.Length.CompareTo(a.Length)); rendered.Clear();
        }
    }
    public static string Template(string source, IDictionary<string,string> values)
    {
        string translated = exact.TryGetValue(source ?? "",out var phrase) ? phrase.translated : source ?? "";
        return Token.Replace(translated, match => values.TryGetValue(match.Groups[1].Value,out string value) ? value : match.Value);
    }
    private static string Render(string source, int depth)
    {
        if (string.IsNullOrEmpty(source) || Code == "ko" || !Ready || depth > 10 || literals.Contains(source)) { return source ?? ""; }
        if (rendered.TryGetValue(source,out string cached)) { return cached; }
        if (exact.TryGetValue(source,out var phrase)) { return Cache(source,phrase.translated); }
        if (!HasKorean(source)) { return source; }
        if (depth == 0 && source.IndexOf('일') > 0)
        {
            string stamped = DayStamp.Replace(source, Code == "ja" ? "${d}日目 ${t}" : "Day ${d} ${t}");
            if (stamped != source) { return Cache(source,Render(stamped,depth+1)); }
        }
        foreach (var candidate in patterns)
        {
            if (!source.Contains(candidate.hint)) { continue; }
            Match match;
            try { match = candidate.pattern.Match(source); }
            catch (RegexMatchTimeoutException) { continue; }
            if (!match.Success) { continue; }
            var values = new Dictionary<string,string>();
            for (int i=0;i<candidate.slots.Length;i++) { values[candidate.slots[i]] = Render(match.Groups["v"+i].Value,depth+1); }
            string result = Token.Replace(candidate.translated, slot => values.TryGetValue(slot.Groups[1].Value,out string value) ? value : slot.Value);
            return Cache(source,result);
        }
        if (source.IndexOf('<') >= 0 || source.IndexOf('\n') >= 0 || source.IndexOf('\r') >= 0)
        {
            string[] parts = Segments.Split(source);
            if (parts.Length > 1)
            {
                var pieces = new StringBuilder();
                for (int i = 0; i < parts.Length; i++) { pieces.Append((i & 1) != 0 ? parts[i] : Render(parts[i],depth+1)); }
                return Cache(source,pieces.ToString());
            }
        }
        var output = new StringBuilder(source.Length + 24);
        int index = 0;
        while (index < source.Length)
        {
            string protectedName = null;
            if (literalSpans.TryGetValue(source[index],out var names))
            {
                foreach (string name in names)
                {
                    if (index + name.Length <= source.Length && string.CompareOrdinal(source,index,name,0,name.Length) == 0)
                    { protectedName = name; break; }
                }
            }
            if (protectedName != null) { output.Append(protectedName); index += protectedName.Length; continue; }
            if (source[index] == '<')
            {
                var tag = Markup.Match(source,index);
                if (tag.Success && tag.Index == index) { output.Append(tag.Value); index += tag.Length; continue; }
            }
            var node = trie; Phrase best = null; int end = index;
            for (int i=index;i<source.Length && node.children.TryGetValue(source[i],out node);i++)
            {
                if (node.phrase == null) { continue; }
                // A literal such as "0돈" must not match the last digit of "250,000돈".
                if (char.IsDigit(source[index]) && index > 0 && (char.IsDigit(source[index-1]) || source[index-1] == ',' || source[index-1] == '.')) { continue; }
                best=node.phrase; end=i+1;
            }
            if (best != null) { output.Append(best.translated); index=end; continue; }
            if ((source[index] == '돈' || source[index] == '주') && index > 0 && (char.IsDigit(source[index-1]) || source[index-1] == ')' || "kKMBT".IndexOf(source[index-1]) >= 0))
            { output.Append(source[index] == '돈' ? Code == "ja" ? "ドン" : " DON" : Code == "ja" ? "株" : " sh."); index++; continue; }
            output.Append(source[index++]);
        }
        string final = output.ToString();
        return Cache(source,final);
    }
    private static string Cache(string source,string value)
    {
        if (rendered.Count >= 2048) { rendered.Clear(); }
        rendered[source] = value; return value;
    }
    public static bool HasKorean(string value)
    {
        foreach (char c in value ?? "") { if (c >= '\uac00' && c <= '\ud7a3') { return true; } }
        return false;
    }
    private void SceneLoaded(Scene scene,LoadSceneMode mode) { ScanScene(scene); }
    public static void ScanScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) { return; }
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) { GameLocalizedText.Attach(label); }
            foreach (var label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true)) { GameLocalizedText.Attach(label); }
        }
    }
    private void OnDestroy()
    {
        if (instance != this) { return; }
        LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
        SceneManager.sceneLoaded -= SceneLoaded; instance = null;
    }
}
