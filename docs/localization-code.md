# Localization 코드 변경 상세

이 작업 시작 시 보관한 파일과 비교했습니다. 이전 작업의 변경은 원본 쪽에 포함됩니다.

## 신규 스크립트 전문

### Assets/Scripts/Localization\GameLocale.cs

```csharp
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
                // A literal such as "0원" must not match the last digit of "250,000원".
                if (char.IsDigit(source[index]) && index > 0 && (char.IsDigit(source[index-1]) || source[index-1] == ',' || source[index-1] == '.')) { continue; }
                best=node.phrase; end=i+1;
            }
            if (best != null) { output.Append(best.translated); index=end; continue; }
            if ((source[index] == '원' || source[index] == '주') && index > 0 && (char.IsDigit(source[index-1]) || source[index-1] == ')' || "kKMBT".IndexOf(source[index-1]) >= 0))
            { output.Append(source[index] == '원' ? Code == "ja" ? "ウォン" : " KRW" : Code == "ja" ? "株" : " sh."); index++; continue; }
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

```

### Assets/Scripts/Localization\GameLocaleFonts.cs

```csharp
using TMPro;
using UnityEngine;

public sealed class GameLocaleFonts : ScriptableObject
{
    public TMP_FontAsset japanese;
    public Font japaneseLegacy;
    public static GameLocaleFonts Current { get; private set; }
    public static void Load()
    {
        Current = Resources.Load<GameLocaleFonts>("Localization/Fonts");
        if (Current == null || Current.japanese == null) { return; }
        if (!TMP_Settings.fallbackFontAssets.Contains(Current.japanese)) { TMP_Settings.fallbackFontAssets.Add(Current.japanese); }
    }
}

```

### Assets/Scripts/Localization\GameLocalizedText.cs

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Stores the original display value, refreshes only when it or the locale changes. Never binds input contents.</summary>
public sealed class GameLocalizedText : MonoBehaviour
{
    private static readonly Dictionary<Component,GameLocalizedText> bindings = new();
    private TMP_Text tmp;
    private Text legacy;
    private string source;
    private bool literal;
    private Font originalFont;
    private TMP_InputField tmpInput;
    private InputField legacyInput;
    private static Font japaneseLegacy;
    public string Source => source;
    public static bool Matches(TMP_Text label,string value)
    { return label != null && (bindings.TryGetValue(label,out var binding) ? binding.source == value : label.text == value); }
    public static bool Matches(Text label,string value)
    { return label != null && (bindings.TryGetValue(label,out var binding) ? binding.source == value : label.text == value); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { bindings.Clear(); japaneseLegacy = null; }
    public static GameLocalizedText Attach(TMP_Text label)
    {
        if (label == null) { return null; }
        if (bindings.TryGetValue(label,out var existing)) { return existing; }
        var input = label.GetComponentInParent<TMP_InputField>();
        if (input != null && input.textComponent == label) { return null; }
        var binding = label.GetComponent<GameLocalizedText>();
        if (binding == null) { binding = label.gameObject.AddComponent<GameLocalizedText>(); }
        binding.tmp = label; binding.tmpInput = input; binding.source = label.text; bindings[label] = binding;
        binding.Refresh(); return binding;
    }
    public static GameLocalizedText Attach(Text label)
    {
        if (label == null) { return null; }
        if (bindings.TryGetValue(label,out var existing)) { return existing; }
        var input = label.GetComponentInParent<InputField>();
        if (input != null && input.textComponent == label) { return null; }
        var binding = label.GetComponent<GameLocalizedText>();
        if (binding == null) { binding = label.gameObject.AddComponent<GameLocalizedText>(); }
        binding.legacy = label; binding.legacyInput = input; binding.originalFont = label.font; binding.source = label.text; bindings[label] = binding;
        binding.Refresh(); return binding;
    }
    public static void Set(TMP_Text label,string value,bool literal=false)
    {
        var binding = Attach(label);
        if (binding == null) { if (label != null && label.text != value) { label.text = value; } return; }
        binding.Assign(value,literal);
    }
    public static void Set(Text label,string value,bool literal=false)
    {
        var binding = Attach(label);
        if (binding == null) { if (label != null && label.text != value) { label.text = value; } return; }
        binding.Assign(value,literal);
    }
    private void Assign(string value,bool isLiteral)
    {
        value ??= "";
        if (source == value && literal == isLiteral) { return; }
        source = value; literal = isLiteral; Refresh();
    }
    private void OnEnable() { GameLocale.Changed += Refresh; Refresh(); }
    private void OnDisable() { GameLocale.Changed -= Refresh; }
    private void Refresh()
    {
        if (source == null) { return; }
        // Runtime UI factories may assign textComponent after creating its label. Never translate/reset user input.
        if (tmpInput != null && tmpInput.textComponent == tmp || legacyInput != null && legacyInput.textComponent == legacy) { return; }
        string value = literal ? source : GameLocale.Text(source);
        if (tmp != null && tmp.text != value) { tmp.text = value; }
        if (legacy != null)
        {
            if (GameLocale.Code == "ja")
            {
                japaneseLegacy ??= GameLocaleFonts.Current?.japaneseLegacy;
                if (japaneseLegacy != null) { legacy.font = japaneseLegacy; }
            }
            else if (originalFont != null) { legacy.font = originalFont; }
            if (legacy.text != value) { legacy.text = value; }
        }
    }
    private void OnDestroy()
    {
        if (tmp != null) { bindings.Remove(tmp); }
        if (legacy != null) { bindings.Remove(legacy); }
    }
}

```

### Assets/Scripts/Localization\LocalizedChat.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>Re-renders the chosen historical utterance; never rerolls speech or reads a current account balance.</summary>
public static class LocalizedChat
{
    public static string Text(BotChatMessage message)
    { return Text(message.Social,message.Text); }
    public static string Text(SocialMessage social,string source)
    {
        if (GameLocale.Code == "ko" || social == null || string.IsNullOrEmpty(social.localizedPhrase) || social.localizedValues == null)
        { return GameLocale.Text(source); }
        var values = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var slot in social.localizedValues)
        {
            string value = slot.value ?? "";
            if (slot.name == "target" || slot.name == "departedName")
            { value = slot.value == "방장" ? GameLocale.Text("방장") : value; }
            else if (slot.name == "gain" || slot.name == "loss")
            { value = GameLocale.Code == "ja" ? Amount(slot.raw,false) + "ウォン" : Amount(slot.raw,false).Replace("~","~₩"); }
            else if (slot.name == "return" || slot.name == "moveTalk") { value = Percent(slot.raw); }
            else if (slot.approximate && (slot.name == "amount" || slot.name == "memAmount")) { value = Amount(slot.raw); }
            else if (slot.approximate && (slot.name == "pct" || slot.name == "memPct"))
            { value = Percent(slot.raw).Replace("%",""); }
            else if (!string.IsNullOrEmpty(slot.raw) && (slot.name == "amount" || slot.name == "memAmount" || slot.name == "pct" || slot.name == "memPct"))
            { value = slot.raw; }
            else { value = GameLocale.Text(value); }
            values[slot.name] = value;
        }
        string text = GameLocale.Template(social.localizedPhrase,values);
        string prefix = string.IsNullOrEmpty(social.localizedTarget) ? "" : "@" +
            (social.localizedTarget == "방장" ? GameLocale.Text("방장") : social.localizedTarget) + " ";
        return prefix + text + GameLocale.Text(social.localizedSuffix);
    }
    private static string Percent(string raw)
    {
        string result = ChatSocialNumbers.Return(raw,1);
        return result.Contains("미만") ? "<" + result.Replace(" 미만","") : result;
    }
    private static string Amount(string raw,bool signed = true)
    {
        if (!decimal.TryParse(raw,NumberStyles.Number,CultureInfo.InvariantCulture,out decimal amount)) { return raw ?? ""; }
        decimal value = Math.Abs(amount), step = value >= 10000m ? 10000m : value >= 1000m ? 1000m : value >= 100m ? 100m : 1m;
        while (step >= 10000m && step <= value / 10m) { step *= 10m; }
        value = Math.Floor(value / step) * step;
        string sign = !signed ? "" : amount < 0 ? "−" : amount > 0 ? "+" : "";
        if (GameLocale.Code == "ja")
        {
            decimal unit = value >= 100000000m ? 100000000m : value >= 10000m ? 10000m : 1m;
            string suffix = unit == 100000000m ? "億" : unit == 10000m ? "万" : "";
            return sign + "約" + (value / unit).ToString("0.#",CultureInfo.InvariantCulture) + suffix;
        }
        decimal scale = value >= 1000000000m ? 1000000000m : value >= 1000000m ? 1000000m : value >= 1000m ? 1000m : 1m;
        string label = scale == 1000000000m ? "b" : scale == 1000000m ? "m" : scale == 1000m ? "k" : "";
        return sign + "~" + (value / scale).ToString("0.##",CultureInfo.InvariantCulture) + label;
    }
}

```

### Assets/Editor/DonDonLocalizationSetup.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;

/// <summary>Initial import into editable Unity String Tables. Reimport is an explicit menu action.</summary>
public static class DonDonLocalizationSetup
{
    private const string Root = "Assets/Localization";
    [MenuItem("DonDon/Localization/Import reviewed translations")]
    public static void Import()
    {
        Directory.CreateDirectory(Root + "/Tables"); Directory.CreateDirectory(Root + "/Locales");
        Directory.CreateDirectory("Assets/Resources/Localization");
        AssetDatabase.Refresh();
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings,Root + "/Localization Settings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }
        var locales = new List<Locale>();
        foreach (string code in new[] { "ko", "en", "ja" })
        {
            string path = Root + "/Locales/" + code + ".asset";
            var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null) { locale = Locale.CreateLocale(code); AssetDatabase.CreateAsset(locale,path); }
            LocalizationEditorSettings.AddLocale(locale); locales.Add(locale);
        }
        settings.GetStartupLocaleSelectors().Clear();
        settings.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier("ko") });
        var collection = LocalizationEditorSettings.GetStringTableCollection(GameLocale.TableName);
        if (collection == null) { collection = LocalizationEditorSettings.CreateStringTableCollection(GameLocale.TableName,Root+"/Tables",locales); }
        var rows = ReadCsv(File.ReadAllText(Root + "/Import/DonDon.csv"));
        if (rows.Count < 2 || rows[0].Length < 5) { throw new InvalidDataException("Invalid translation CSV."); }
        foreach (var locale in locales)
        {
            var table = collection.GetTable(locale.Identifier) as StringTable;
            if (table == null) { table = collection.AddNewTable(locale.Identifier) as StringTable; }
            int column = locale.Identifier.Code == "ko" ? 2 : locale.Identifier.Code == "en" ? 3 : 4;
            foreach (var row in rows.Skip(1))
            {
                if (row.Length < 5 || string.IsNullOrEmpty(row[column])) { throw new InvalidDataException("Missing translation: " + row[0]); }
                var entry = table.AddEntry(row[0],row[column]);
                entry.IsSmart = false;
            }
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(collection.SharedData); EditorUtility.SetDirty(settings);
        BuildFonts(rows);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Localization imported: " + (rows.Count-1) + " entries, ko/en/ja.");
    }
    private static void BuildFonts(List<string[]> rows)
    {
        const string fontPath = "Assets/Resources/Localization/DonDon Japanese SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Localization/NotoSansCJKjp-Regular.otf");
        if (source == null) { throw new InvalidDataException("Japanese source font missing."); }
        if (font == null)
        {
            font = TMP_FontAsset.CreateFontAsset(source,64,8,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            font.name = "DonDon Japanese SDF"; font.isMultiAtlasTexturesEnabled = true;
            AssetDatabase.CreateAsset(font,fontPath);
            AssetDatabase.AddObjectToAsset(font.material,font);
            foreach (var atlas in font.atlasTextures) { AssetDatabase.AddObjectToAsset(atlas,font); }
        }
        string corpus = new string(string.Concat(rows.Skip(1).Select(row=>row[4])).Distinct().ToArray());
        // Keep the reviewed Japanese glyphs warm in player content; avoid an initial atlas rebuild.
        var fontSettings = new SerializedObject(font);
        fontSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        fontSettings.ApplyModifiedPropertiesWithoutUndo();
        // Align fallback line metrics with Pretendard. Noto's default 1.45em line hides
        // Japanese glyphs in the existing single-line trading rows (1.2em tall).
        var reference = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        if (reference != null)
        {
            var face = font.faceInfo; var metrics = reference.faceInfo;
            float ratio = face.pointSize / metrics.pointSize;
            face.ascentLine = metrics.ascentLine * ratio; face.descentLine = metrics.descentLine * ratio;
            face.lineHeight = metrics.lineHeight * ratio; font.faceInfo = face;
        }
        font.TryAddCharacters(corpus,out string missing);
        if (!string.IsNullOrEmpty(missing)) { Debug.LogWarning("Japanese font missing: " + missing); }
        foreach (var atlas in font.atlasTextures)
        { if (!AssetDatabase.Contains(atlas)) { AssetDatabase.AddObjectToAsset(atlas,font); } }
        const string resource = "Assets/Resources/Localization/Fonts.asset";
        var fonts = AssetDatabase.LoadAssetAtPath<GameLocaleFonts>(resource);
        if (fonts == null) { fonts = ScriptableObject.CreateInstance<GameLocaleFonts>(); AssetDatabase.CreateAsset(fonts,resource); }
        fonts.japanese = font; fonts.japaneseLegacy = source;
        EditorUtility.SetDirty(font); EditorUtility.SetDirty(fonts);
        Directory.CreateDirectory("Assets/StreamingAssets/ThirdPartyLicenses");
        File.Copy("Assets/Fonts/Localization/OFL.txt","Assets/StreamingAssets/ThirdPartyLicenses/NotoSansCJK-OFL.txt",true);
    }
    public static List<string[]> ReadCsv(string text)
    {
        var rows = new List<string[]>(); var cells = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
        for (int i=0;i<text.Length;i++)
        {
            char c = text[i];
            if (c == '"')
            {
                if (quoted && i+1<text.Length && text[i+1]=='"') { cell.Append('"'); i++; }
                else { quoted = !quoted; }
            }
            else if (!quoted && c == ',') { cells.Add(cell.ToString()); cell.Clear(); }
            else if (!quoted && (c == '\r' || c == '\n'))
            {
                if (c == '\r' && i+1<text.Length && text[i+1]=='\n') { i++; }
                cells.Add(cell.ToString()); cell.Clear(); rows.Add(cells.ToArray()); cells.Clear();
            }
            else { cell.Append(c); }
        }
        if (quoted) { throw new InvalidDataException("Unclosed CSV field."); }
        if (cell.Length>0 || cells.Count>0) { cells.Add(cell.ToString()); rows.Add(cells.ToArray()); }
        return rows;
    }
    [MenuItem("DonDon/Localization/Preview Korean")]
    private static void Korean() { Select("ko"); }
    [MenuItem("DonDon/Localization/Preview English")]
    private static void English() { Select("en"); }
    [MenuItem("DonDon/Localization/Preview Japanese")]
    private static void Japanese() { Select("ja"); }
    private static void Select(string code)
    {
        if (EditorApplication.isPlaying) { GameLocale.SetLanguage(code); }
        else { UnityEditor.Localization.LocalizationEditorSettings.ActiveLocalizationSettings.SetSelectedLocale(LocalizationEditorSettings.GetLocales().First(locale=>locale.Identifier.Code==code)); }
    }
    public static void RunImport()
    {
        try { Import(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}

```

### Assets/Editor/DonDonLocalizationValidation.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit, single-process Play Mode validation. Does not save the scene or game.</summary>
public static class DonDonLocalizationValidation
{
    private static readonly string[] Languages = { "ko", "en", "ja" };
    private static readonly string[] Views = { "chart", "stocks", "chat", "dashboard", "orders", "news", "settlement", "phone" };
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string folder, previousLanguage;
    private static bool previousExists, oldOptions, created, finished;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static int phase, language, view, checks;
    private static float due;
    private static Camera camera;
    private static TabletAccountUI tablet;
    private static string orderInput;
    private static long articleId;
    private static float articlePosition;
    private static Vector3 canvasPosition;
    private static Quaternion canvasRotation;
    private static Camera eventCamera;
    [Serializable] private sealed class Row { public string path, source, text; public bool overflow, truncated; public float width, height, font; }
    [Serializable] private sealed class Rows { public List<Row> rows = new(); }
    private static void Check(bool ok,string label)
    {
        File.AppendAllText(Path.Combine(folder,"checks.txt"),(ok ? "PASS " : "FAIL ")+label+"\n");
        if (!ok) { throw new InvalidOperationException(label); }
        checks++;
    }
    public static void Run()
    {
        DonDonLocalizationSetup.Import();
        folder = Environment.GetEnvironmentVariable("DONDON_LOCALIZATION_OUTPUT");
        if (string.IsNullOrEmpty(folder) || !Path.GetFullPath(folder).StartsWith(Path.GetFullPath(".")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Validation output must be inside this project."); }
        Directory.CreateDirectory(folder);
        previousExists = PlayerPrefs.HasKey("DonDon.Language"); previousLanguage = PlayerPrefs.GetString("DonDon.Language","ko");
        PlayerPrefs.SetString("DonDon.Language","ko");
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update += Monitor; Application.logMessageReceived += Log;
        EditorApplication.EnterPlaymode();
    }
    public static void BuildContent()
    {
        try
        {
            DonDonLocalizationSetup.Import();
            var font = Resources.Load<TMP_FontAsset>("Localization/DonDon Japanese SDF");
            int glyphs = font.characterTable.Count;
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error)) { throw new InvalidOperationException(result.Error); }
            if (glyphs < 500 || font.characterTable.Count != glyphs) { throw new InvalidOperationException("Japanese warm glyphs were lost during build."); }
            string output = Environment.GetEnvironmentVariable("DONDON_LOCALIZATION_OUTPUT");
            File.WriteAllText(Path.Combine(output,"content-build.txt"),"PASS: Addressables player content built.\nPASS: "+glyphs+" warm Japanese characters retained.\n"+result.OutputPath);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void ValidateHeading()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity"); GameLocaleFonts.Load();
            var label = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include).First(t=>t.name == "Text :: Operation Monitor Heading");
            string output = Environment.GetEnvironmentVariable("DONDON_LOCALIZATION_OUTPUT");
            foreach (string text in new[] { "체결 흐름", "Trade flow", "約定の流れ" })
            {
                label.text = text; Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate(true);
                if (label.isTextOverflowing || label.isTextTruncated) { throw new InvalidOperationException("Caption overflow: "+text); }
                File.AppendAllText(Path.Combine(output,"heading.txt"),"PASS "+text+" at "+label.fontSize+"\n");
            }
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static void Log(string message,string stack,LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        { File.AppendAllText(Path.Combine(folder,"errors.txt"),message+"\n"+stack+"\n"); }
    }
    private static void Monitor()
    {
        if (finished) { return; }
        if (EditorApplication.timeSinceStartup-began > 330) { Finish("Timeout"); return; }
        if (EditorApplication.isPlaying && !created)
        { created = true; new GameObject("Test :: Locale Capture").AddComponent<DonDonLocalizationProbe>(); }
    }
    public static void Step()
    {
        if (finished || !GameLocale.Ready || Time.unscaledTime < due) { return; }
        try
        {
            camera = DeskViewController.Instance?.ViewCamera ?? Camera.main;
            var setup = Object.FindAnyObjectByType<PlayerCompanySetupUI>();
            if (phase == 0)
            {
                if (setup == null || setup.businessName == null) { return; }
                Check(LocalizationSettings.AvailableLocales.Locales.Count == 3,"only ko/en/ja locales");
                setup.businessName.SetTextWithoutNotify("안전 수익 회사");
                GameLocale.SetLanguage(Languages[language]); phase = 1; due = Time.unscaledTime+.7f; return;
            }
            if (phase == 1)
            {
                if (GameLocale.Code != Languages[language]) { return; }
                Check(setup.businessName.text == "안전 수익 회사","input unchanged: "+GameLocale.Code);
                Check(GameLocale.MissingSources.Count == 0,"no missing String Table entries: "+GameLocale.Code);
                Check(!GameLocale.Supported("zh") && !GameLocale.Supported("fr"),"unsupported languages excluded");
                Capture("setup-"+GameLocale.Code,1920,1080); Dump("setup-"+GameLocale.Code);
                if (++language < Languages.Length) { phase = 0; return; }
                setup.Refresh(); setup.confirm.onClick.Invoke(); phase = 2; due = Time.unscaledTime+3; return;
            }
            if (phase == 2)
            {
                if (!StockManager.Instance.SetupConfirmed || !StockManager.Instance.EconomyInitialized) { return; }
                tablet = Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include).First(t => !t.second);
                Check(GameLocale.Text(DailyIncomeController.Instance.CompanyName) == "안전 수익 회사","player company name preserved");
                Check(Time.timeScale == 1,"no simulation speedup");
                language = 0; view = 0; phase = 3; return;
            }
            if (phase == 3)
            {
                GameLocale.SetLanguage(Languages[language]); phase = 4; due = Time.unscaledTime+.8f; return;
            }
            if (phase == 4)
            {
                if (GameLocale.Code != Languages[language]) { return; }
                Check(GameLocale.Text("안전 수익 회사") == "안전 수익 회사","literal protected after switch: "+GameLocale.Code);
                ValidateChat(); OpenView(); phase = 5; due = Time.unscaledTime+1; return;
            }
            if (phase == 5)
            {
                if (DeskViewController.Instance.IsTransitioning) { return; }
                string name = GameLocale.Code+"-"+Views[view];
                Capture(name,1600,900); Capture(name,1920,1080); Dump(name);
                if (++view < Views.Length) { OpenView(); due = Time.unscaledTime+.8f; return; }
                ExportChat();
                if (++language < Languages.Length) { view = 0; phase = 3; return; }
                PhoneCallController.Instance.Lower(); DeskViewController.Instance.Focus(DeskViewTarget.TabletOne);
                tablet.ShowPage(TabletAccountUI.Page.Events);
                var reader = tablet.events.newsReader;
                var scroll = (ScrollRect)typeof(NewsDeskUI).GetField("articleScroll",Private).GetValue(reader);
                scroll.verticalNormalizedPosition = .4f; articlePosition = scroll.verticalNormalizedPosition;
                articleId = reader.SelectedArticle.id; orderInput = tablet.orders.price.text;
                var canvas = tablet.GetComponentInParent<Canvas>();
                canvasPosition = canvas.transform.localPosition; canvasRotation = canvas.transform.localRotation; eventCamera = canvas.worldCamera;
                GameLocale.SetLanguage("en"); phase = 6; due = Time.unscaledTime+1.5f;
            }
            else if (phase == 6)
            {
                var reader = tablet.events.newsReader;
                var scroll = (ScrollRect)typeof(NewsDeskUI).GetField("articleScroll",Private).GetValue(reader);
                Check(reader.SelectedArticle.id == articleId,"article selection survives language change");
                Check(Mathf.Abs(scroll.verticalNormalizedPosition-articlePosition)<.01f,"article scroll survives language change");
                Check(tablet.orders.price.text == orderInput,"order input survives language change");
                var canvas = tablet.GetComponentInParent<Canvas>();
                Check(canvas.transform.localPosition == canvasPosition && canvas.transform.localRotation == canvasRotation && canvas.worldCamera == eventCamera,"Canvas local pose and Event Camera unchanged during existing camera animation");
                string oldQuery = (string)typeof(NewsDeskUI).GetField("query",Private).GetValue(reader);
                typeof(NewsDeskUI).GetField("query",Private).SetValue(reader,GameLocale.Text(reader.SelectedArticle.title));
                Check((bool)typeof(NewsDeskUI).GetMethod("MatchesQuery",Private).Invoke(reader,new object[] { reader.SelectedArticle }),"localized headline search matches original article");
                typeof(NewsDeskUI).GetField("query",Private).SetValue(reader,oldQuery);
                Check(PlayerPrefs.GetString("DonDon.Language") == "en","language preference persisted separately from world save");
                Check(!File.Exists(Path.Combine(folder,"errors.txt")),"no Unity runtime errors"); Finish(null);
            }
        }
        catch (Exception e) { Finish(e.ToString()); }
    }
    private static void ValidateChat()
    {
        decimal cash = tablet.account.cash, reserved = tablet.account.ReservedCash;
        var social = new SocialMessage { localizedPhrase = "{stock} {return}인데 아직 안 팔았음. {gain} 찍힘 ㅋㅋ", localizedSuffix = "", localizedValues = new[] {
            new SocialSpeechValue { name = "stock", value = "대만디스플레이", raw = "대만디스플레이" },
            new SocialSpeechValue { name = "return", value = "+50퍼", raw = "50" },
            new SocialSpeechValue { name = "gain", value = "500장", raw = "5284003" } } };
        // The chosen phrase itself is looked up from the actual catalogue below; no simulated speech is injected.
        var source = Resources.Load<TextAsset>("Localization/SourceIndex");
        var entry = JsonUtility.FromJson<GameLocale.SourceCatalog>(source.text).entries.First(e => e.source.Contains("{gain}") && e.source.Contains("{symbol}") && !e.source.Contains("{positionState}"));
        social.localizedPhrase = entry.source;
        social.localizedValues = social.localizedValues.Concat(new[] { new SocialSpeechValue { name = "symbol",value = "DDP" },new SocialSpeechValue { name = "profitKind",value = "평가" } }).ToArray();
        string before = JsonUtility.ToJson(social), state = JsonUtility.ToJson(UnityEngine.Random.state);
        string rendered = LocalizedChat.Text(social,"한국어 원문");
        Check(before == JsonUtility.ToJson(social) && state == JsonUtility.ToJson(UnityEngine.Random.state),"render preserves facts and RNG: "+GameLocale.Code);
        if (GameLocale.Code == "ko") { Check(rendered == "한국어 원문","Korean source unchanged"); }
        else
        {
            Check(GameLocale.Text("장 마감 회사 수익 +250,000원").Contains("250,000"),"currency translation preserves every digit");
            Check(!rendered.Contains("5284003") && !rendered.Contains("5,284,003"),"community profit is approximate: "+GameLocale.Code);
            Check(!rendered.Contains("{gain}") && !rendered.Contains("{stock}"),"snapshot slots resolved: "+GameLocale.Code);
            Check(rendered.Contains(GameLocale.Code == "en" ? "5m" : "500万"),"KRW magnitude preserved: "+GameLocale.Code);
        }
        File.AppendAllText(Path.Combine(folder,"snapshot-test.txt"),GameLocale.Code+": "+rendered+"\n");
        Check(tablet.account.cash == cash && tablet.account.ReservedCash == reserved,"translation does not change account or reservations");
        var stock = StockManager.Instance.stocks.First(s => s.symbol == "DDP");
        string displayName = GameLocale.Text(stock.stockName);
        var mentions = new ChatSocialMentions(Array.Empty<string>(),new[] { new KeyValuePair<string,string>(displayName,stock.symbol) });
        string link = mentions.Format(displayName+(GameLocale.Code == "ja" ? "はどう？" : "?"));
        Check(link.Contains("<link=\"stock:") && ChatSocialMentions.TryStockSymbol("stock:"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(stock.symbol)),out string symbol) && symbol == stock.symbol,"translated stock mention retains selection identity: "+GameLocale.Code);
        if (GameLocale.Code == "ja")
        {
            Check(mentions.Format("今日の"+displayName+"はどう？").Contains("<link="),"Japanese stock links work without spaces");
            Check(!mentions.Format(displayName+"HD").Contains("<link="),"stock link does not match longer ticker name");
        }
    }
    private static void OpenView()
    {
        PhoneCallController.Instance?.Lower();
        var statement = tablet.dashboard.GetComponent<DailyIncomeUI>();
        typeof(DailyIncomeUI).GetMethod("Close",Private)?.Invoke(statement,null);
        var desk = DeskViewController.Instance;
        if (view < 3) { desk.Focus(view == 0 ? DeskViewTarget.Chart : view == 1 ? DeskViewTarget.Stocks : DeskViewTarget.Chat); return; }
        desk.Focus(DeskViewTarget.TabletOne);
        tablet.ShowPage(view == 4 ? TabletAccountUI.Page.Orders : view == 5 ? TabletAccountUI.Page.Events : TabletAccountUI.Page.Dashboard);
        if (view == 6) { typeof(DailyIncomeUI).GetMethod("Open",Private).Invoke(statement,null); }
        if (view == 7) { desk.ExitView(true); PhoneCallController.Instance.Ring(PhoneCallController.Caller.Research); PhoneCallController.Instance.Raise(); }
    }
    private static void ExportChat()
    {
        var market = MarketDayController.Instance;
        for (int i = Math.Max(0,market.MessageCount-25); i < market.MessageCount; i++)
        {
            var message = market.GetMessage(i);
            File.AppendAllText(Path.Combine(folder,"actual-chat-"+GameLocale.Code+".txt"),message.Text+"\n=> "+LocalizedChat.Text(message)+"\n\n");
        }
    }
    private static string PathOf(Transform value) => value.parent == null ? value.name : PathOf(value.parent)+"/"+value.name;
    private static void Dump(string name)
    {
        var rows = new Rows(); Canvas.ForceUpdateCanvases();
        foreach (var label in Object.FindObjectsByType<TMP_Text>())
        {
            if (!label.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(label.text)) { continue; }
            label.ForceMeshUpdate();
            rows.rows.Add(new Row { path = PathOf(label.transform),source = label.GetComponent<GameLocalizedText>()?.Source,
                text = label.text, overflow = label.isTextOverflowing,truncated = label.isTextTruncated,width = label.rectTransform.rect.width,height = label.rectTransform.rect.height,font = label.fontSize });
        }
        File.WriteAllText(Path.Combine(folder,name+"-text.json"),JsonUtility.ToJson(rows,true));
        if (name.EndsWith("-orders"))
        {
            var important = tablet.orders.panel.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy && !string.IsNullOrEmpty(t.text));
            Check(important.All(t => !t.isTextOverflowing && !t.isTextTruncated),"order controls fit without truncation: "+GameLocale.Code);
        }
    }
    private static void Capture(string name,int width,int height)
    {
        var overlays = new List<(Canvas canvas,Camera camera,float distance)>();
        var target = new RenderTexture(width,height,24);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active; Texture2D pixels = null;
        try
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) { continue; }
                overlays.Add((canvas,canvas.worldCamera,canvas.planeDistance)); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane+.1f;
            }
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(width,height,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder,name+"-"+width+"x"+height+".png"),pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            if (pixels != null) { Object.Destroy(pixels); }
            target.Release(); Object.Destroy(target);
            foreach (var saved in overlays)
            { saved.canvas.renderMode = RenderMode.ScreenSpaceOverlay; saved.canvas.worldCamera = saved.camera; saved.canvas.planeDistance = saved.distance; }
        }
    }
    private static void Finish(string failure)
    {
        if (finished) { return; }
        finished = true;
        File.WriteAllText(Path.Combine(folder,"result.txt"),"Checks: "+checks+"\n"+(failure ?? "PASS"));
        if (previousExists) { PlayerPrefs.SetString("DonDon.Language",previousLanguage); } else { PlayerPrefs.DeleteKey("DonDon.Language"); }
        PlayerPrefs.Save();
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags;
        EditorApplication.update -= Monitor; Application.logMessageReceived -= Log;
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
[DefaultExecutionOrder(32000)]
public sealed class DonDonLocalizationProbe : MonoBehaviour
{
    private void Awake() { Application.runInBackground = true; Application.targetFrameRate = 20; QualitySettings.vSyncCount = 0; }
    private void LateUpdate() { DonDonLocalizationValidation.Step(); }
}

```

## 수정한 함수의 원본과 수정본

일반 UI 함수의 변경은 직접 text 대입을 GameLocalizedText.Set으로, 값 비교를 Matches로, 레이아웃 측정을 번역된 텍스트 기준으로 연결하는 것입니다. 거래 계산과 버튼 이벤트는 그대로 유지합니다.

### Assets/Scripts/ChartCandleSelectData.cs

#### SetTxt

원본:

```csharp
static void SetTxt(TextMeshProUGUI t, string s, float? value, float? previousClose)
    {
        if (t == null) return;
        t.text = s;
        if (value.HasValue && previousClose.HasValue)
            t.color = value.Value > previousClose.Value ? Up : (value.Value < previousClose.Value ? Down : Color.white);
    }
```

수정본:

```csharp
static void SetTxt(TextMeshProUGUI t, string s, float? value, float? previousClose)
    {
        if (t == null) return;
        GameLocalizedText.Set(t,s);
        if (value.HasValue && previousClose.HasValue)
            t.color = value.Value > previousClose.Value ? Up : (value.Value < previousClose.Value ? Down : Color.white);
    }
```

#### SetTxt

원본:

```csharp
static void SetTxt(TextMeshProUGUI t, string s)
    {
        if (t != null) t.text = s;
    }
```

수정본:

```csharp
static void SetTxt(TextMeshProUGUI t, string s)
    {
        if (t != null) GameLocalizedText.Set(t,s);
    }
```

### Assets/Scripts/ChartManager.cs

#### RefreshSymbolAndName

원본:

```csharp
void RefreshSymbolAndName()
    {
        if (currentStock == null)
        { return; }

        if (textSymbol != null)
        {
            textSymbol.text = currentStock.symbol;
        }

        if (textName != null)
        {
            textName.text = currentStock.stockName;
        }
        if (textRiskBadge != null)
        {
            textRiskBadge.text = currentStock.RiskLabel;
            textRiskBadge.color = currentStock.IsTradingHalted ? new Color32(145, 152, 158, 255) : currentStock.RiskGrade == StockRiskGrade.Safe ? new Color32(127, 165, 140, 255) : currentStock.RiskGrade == StockRiskGrade.Warning ? new Color32(205, 182, 116, 255) : currentStock.RiskGrade == StockRiskGrade.Danger ? new Color32(217, 151, 102, 255) : new Color32(221, 119, 127, 255);
        }
    }
```

수정본:

```csharp
void RefreshSymbolAndName()
    {
        if (currentStock == null)
        { return; }

        if (textSymbol != null)
        {
            GameLocalizedText.Set(textSymbol,currentStock.symbol);
        }

        if (textName != null)
        {
            GameLocalizedText.Set(textName,currentStock.stockName);
        }
        if (textRiskBadge != null)
        {
            GameLocalizedText.Set(textRiskBadge,currentStock.RiskLabel);
            textRiskBadge.color = currentStock.IsTradingHalted ? new Color32(145, 152, 158, 255) : currentStock.RiskGrade == StockRiskGrade.Safe ? new Color32(127, 165, 140, 255) : currentStock.RiskGrade == StockRiskGrade.Warning ? new Color32(205, 182, 116, 255) : currentStock.RiskGrade == StockRiskGrade.Danger ? new Color32(217, 151, 102, 255) : new Color32(221, 119, 127, 255);
        }
    }
```

#### RefreshTradeBasedValues

원본:

```csharp
void RefreshTradeBasedValues()
    {
        if (currentStock == null)
        { return; }

        if (textLastPrice != null)
        {
            textLastPrice.text = PriceTickPolicy.FormatPrice(currentStock.currentPrice);
        }

        decimal refPrice = GetReferencePriceForChangePct();
        decimal change = refPrice > 0m ? currentStock.currentPrice - refPrice : 0m;
        decimal pct = refPrice > 0m ? change / refPrice * 100m : 0m;
        Color changeColor = change > 0m ? new Color32(239, 89, 100, 255) : change < 0m ? new Color32(76, 150, 232, 255) : new Color32(138, 150, 163, 255);
        if (textChangePct != null)
        {
            textChangePct.text = refPrice > 0m ? (pct > 0m ? $"+{pct:N2}%" : $"{pct:N2}%") : "—";
            textChangePct.color = changeColor;
        }
        if (textChangeAmount != null)
        {
            string value = FormatDailyChange(currentStock.currentPrice, refPrice);
            if (textChangeAmount.text != value)
            { textChangeAmount.text = value; }
            if (textChangeAmount.color != changeColor)
            { textChangeAmount.color = changeColor; }
        }

        if (_sessionStats.TryGetValue(currentStock, out var stats))
        {
            if (textVolume != null)
            {
                textVolume.text = stats.volume.ToString("N0");
            }

            if (textTurnover != null)
            {
                textTurnover.text = stats.turnover.ToString("N0");
            }
        }
        else
        {
            if (textVolume != null)
            {
                textVolume.text = "0";
            }

            if (textTurnover != null)
            {
                textTurnover.text = "0";
            }
        }
    }
```

수정본:

```csharp
void RefreshTradeBasedValues()
    {
        if (currentStock == null)
        { return; }

        if (textLastPrice != null)
        {
            GameLocalizedText.Set(textLastPrice,PriceTickPolicy.FormatPrice(currentStock.currentPrice));
        }

        decimal refPrice = GetReferencePriceForChangePct();
        decimal change = refPrice > 0m ? currentStock.currentPrice - refPrice : 0m;
        decimal pct = refPrice > 0m ? change / refPrice * 100m : 0m;
        Color changeColor = change > 0m ? new Color32(239, 89, 100, 255) : change < 0m ? new Color32(76, 150, 232, 255) : new Color32(138, 150, 163, 255);
        if (textChangePct != null)
        {
            GameLocalizedText.Set(textChangePct,refPrice > 0m ? (pct > 0m ? $"+{pct:N2}%" : $"{pct:N2}%") : "—");
            textChangePct.color = changeColor;
        }
        if (textChangeAmount != null)
        {
            string value = FormatDailyChange(currentStock.currentPrice, refPrice);
            if (!GameLocalizedText.Matches(textChangeAmount,value))
            { GameLocalizedText.Set(textChangeAmount,value); }
            if (textChangeAmount.color != changeColor)
            { textChangeAmount.color = changeColor; }
        }

        if (_sessionStats.TryGetValue(currentStock, out var stats))
        {
            if (textVolume != null)
            {
                GameLocalizedText.Set(textVolume,stats.volume.ToString("N0"));
            }

            if (textTurnover != null)
            {
                GameLocalizedText.Set(textTurnover,stats.turnover.ToString("N0"));
            }
        }
        else
        {
            if (textVolume != null)
            {
                GameLocalizedText.Set(textVolume,"0");
            }

            if (textTurnover != null)
            {
                GameLocalizedText.Set(textTurnover,"0");
            }
        }
    }
```

#### RefreshRealFakeValues

원본:

```csharp
void RefreshRealFakeValues()
    {
        if (currentStock == null)
        { return; }

        if (textRealValue != null)
        {
            textRealValue.text = PriceTickPolicy.FormatPrice(currentStock.realValue);
        }

        if (textFakeValue != null)
        {
            textFakeValue.text = PriceTickPolicy.FormatPrice(currentStock.fakeValue);
        }
    }
```

수정본:

```csharp
void RefreshRealFakeValues()
    {
        if (currentStock == null)
        { return; }

        if (textRealValue != null)
        {
            GameLocalizedText.Set(textRealValue,PriceTickPolicy.FormatPrice(currentStock.realValue));
        }

        if (textFakeValue != null)
        {
            GameLocalizedText.Set(textFakeValue,PriceTickPolicy.FormatPrice(currentStock.fakeValue));
        }
    }
```

#### RefreshStatistics

원본:

```csharp
void RefreshStatistics()
    {
        if (currentStock == null || StockStatistics.Instance == null)
        { return; }

        var stats = StockStatistics.Instance.GetStats(currentStock);
        if (stats.stock == null)
        { return; }

        if (text52WeekHigh != null)
        {
            text52WeekHigh.text = PriceTickPolicy.FormatPrice(stats.week52High);
        }

        if (text52WeekLow != null)
        {
            text52WeekLow.text = PriceTickPolicy.FormatPrice(stats.week52Low);
        }

        if (textPrevClose != null)
        {
            textPrevClose.text = PriceTickPolicy.FormatPrice(stats.prevDayClose);
        }

        if (textDayHigh != null)
        {
            textDayHigh.text = PriceTickPolicy.FormatPrice(stats.dayHigh);
        }

        if (textDayHighPct != null && stats.prevDayClose > 0m)
        {
            decimal pct = (stats.dayHigh - stats.prevDayClose) / stats.prevDayClose * 100m;
            textDayHighPct.text = pct >= 0m ? $"+{pct:N2}%" : $"{pct:N2}%";
            textDayHighPct.color = pct >= 0m ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255);
        }

        if (textDayLow != null)
        {
            textDayLow.text = PriceTickPolicy.FormatPrice(stats.dayLow);
        }

        if (textDayLowPct != null && stats.prevDayClose > 0m)
        {
            decimal pct = (stats.dayLow - stats.prevDayClose) / stats.prevDayClose * 100m;
            textDayLowPct.text = pct >= 0m ? $"+{pct:N2}%" : $"{pct:N2}%";
            textDayLowPct.color = pct >= 0m ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255);
        }

        if (textTradeStrength != null)
        {
            float strength = StockStatistics.Instance.GetTradeStrength(currentStock);
            textTradeStrength.text = strength >= 999f ? "999+" : $"{strength:N1}%";
            textTradeStrength.color = strength >= 100f ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255);
        }
    }
```

수정본:

```csharp
void RefreshStatistics()
    {
        if (currentStock == null || StockStatistics.Instance == null)
        { return; }

        var stats = StockStatistics.Instance.GetStats(currentStock);
        if (stats.stock == null)
        { return; }

        if (text52WeekHigh != null)
        {
            GameLocalizedText.Set(text52WeekHigh,PriceTickPolicy.FormatPrice(stats.week52High));
        }

        if (text52WeekLow != null)
        {
            GameLocalizedText.Set(text52WeekLow,PriceTickPolicy.FormatPrice(stats.week52Low));
        }

        if (textPrevClose != null)
        {
            GameLocalizedText.Set(textPrevClose,PriceTickPolicy.FormatPrice(stats.prevDayClose));
        }

        if (textDayHigh != null)
        {
            GameLocalizedText.Set(textDayHigh,PriceTickPolicy.FormatPrice(stats.dayHigh));
        }

        if (textDayHighPct != null && stats.prevDayClose > 0m)
        {
            decimal pct = (stats.dayHigh - stats.prevDayClose) / stats.prevDayClose * 100m;
            GameLocalizedText.Set(textDayHighPct,pct >= 0m ? $"+{pct:N2}%" : $"{pct:N2}%");
            textDayHighPct.color = pct >= 0m ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255);
        }

        if (textDayLow != null)
        {
            GameLocalizedText.Set(textDayLow,PriceTickPolicy.FormatPrice(stats.dayLow));
        }

        if (textDayLowPct != null && stats.prevDayClose > 0m)
        {
            decimal pct = (stats.dayLow - stats.prevDayClose) / stats.prevDayClose * 100m;
            GameLocalizedText.Set(textDayLowPct,pct >= 0m ? $"+{pct:N2}%" : $"{pct:N2}%");
            textDayLowPct.color = pct >= 0m ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255);
        }

        if (textTradeStrength != null)
        {
            float strength = StockStatistics.Instance.GetTradeStrength(currentStock);
            GameLocalizedText.Set(textTradeStrength,strength >= 999f ? "999+" : $"{strength:N1}%");
            textTradeStrength.color = strength >= 100f ? new Color32(239, 89, 100, 255) : new Color32(76, 150, 232, 255);
        }
    }
```

### Assets/Scripts/DeskControlHints.cs

#### Hint

원본:

```csharp
private void Hint(int index, string key, string caption)
    {
        cells[index].gameObject.SetActive(true);
        if (keys[index].text != key)
        { keys[index].text = key; }
        if (captions[index].text != caption)
        { captions[index].text = caption; }
    }
```

수정본:

```csharp
private void Hint(int index, string key, string caption)
    {
        cells[index].gameObject.SetActive(true);
        if (!GameLocalizedText.Matches(keys[index],key))
        { GameLocalizedText.Set(keys[index],key); }
        if (!GameLocalizedText.Matches(captions[index],caption))
        { GameLocalizedText.Set(captions[index],caption); }
    }
```

### Assets/Scripts/PlayerCompanySetupUI.cs

#### Start

원본:

```csharp
void Start()
    {
        var manager = StockManager.Instance;
        if (manager == null || !manager.useInitialEconomy || manager.EconomyInitialized)
        {
            gameObject.SetActive(false);
            return;
        }

        previousCursor = Cursor.lockState;
        previousVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        companyName.richText = false;
        companyName.SetTextWithoutNotify(manager.economy.companies[0].name);
        issuedShares.SetTextWithoutNotify(manager.economy.companies[0].issuedShares.ToString(System.Globalization.CultureInfo.InvariantCulture));
        publicPercentage.SetTextWithoutNotify(manager.economy.defaultPublicPercentage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        companyName.onValueChanged.AddListener(Changed);
        issuedShares.onValueChanged.AddListener(Changed);
        publicPercentage.onValueChanged.AddListener(Changed);
        confirm.onClick.AddListener(Confirm);
        StockManager.OnInitialEconomyReady += OnReady;
        listenersReady = true;
        if (retailIntroduction != null)
        { retailIntroduction.SetActive(manager.economy.retailPlayerStart); }
        if (companySetup != null)
        { companySetup.SetActive(!manager.economy.retailPlayerStart); }
        if (controlsToggle != null)
        { controlsToggle.onClick.AddListener(ToggleControls); controlsToggle.gameObject.SetActive(manager.economy.retailPlayerStart); }
        if (!manager.economy.retailPlayerStart)
        { confirm.GetComponentInChildren<TextMeshProUGUI>().text = "기업 설정하고 시작"; }
        if (manager.economy.retailPlayerStart)
        { companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false; }
        if (manager.economy.retailPlayerStart) { businessName = DailyIncomeUI.CompanyField(this); }
        Refresh();
    }
```

수정본:

```csharp
void Start()
    {
        var manager = StockManager.Instance;
        if (manager == null || !manager.useInitialEconomy || manager.EconomyInitialized)
        {
            gameObject.SetActive(false);
            return;
        }

        previousCursor = Cursor.lockState;
        previousVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        companyName.richText = false;
        companyName.SetTextWithoutNotify(manager.economy.companies[0].name);
        issuedShares.SetTextWithoutNotify(manager.economy.companies[0].issuedShares.ToString(System.Globalization.CultureInfo.InvariantCulture));
        publicPercentage.SetTextWithoutNotify(manager.economy.defaultPublicPercentage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        companyName.onValueChanged.AddListener(Changed);
        issuedShares.onValueChanged.AddListener(Changed);
        publicPercentage.onValueChanged.AddListener(Changed);
        confirm.onClick.AddListener(Confirm);
        StockManager.OnInitialEconomyReady += OnReady;
        listenersReady = true;
        if (retailIntroduction != null)
        { retailIntroduction.SetActive(manager.economy.retailPlayerStart); }
        if (companySetup != null)
        { companySetup.SetActive(!manager.economy.retailPlayerStart); }
        if (controlsToggle != null)
        { controlsToggle.onClick.AddListener(ToggleControls); controlsToggle.gameObject.SetActive(manager.economy.retailPlayerStart); }
        if (!manager.economy.retailPlayerStart)
        { GameLocalizedText.Set(confirm.GetComponentInChildren<TextMeshProUGUI>(),"기업 설정하고 시작"); }
        if (manager.economy.retailPlayerStart)
        { companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false; }
        if (manager.economy.retailPlayerStart) { businessName = DailyIncomeUI.CompanyField(this); }
        Refresh();
    }
```

#### Refresh

원본:

```csharp
public void Refresh()
    {
        var manager = StockManager.Instance;
        bool valid = manager.PreviewPlayer(companyName.text, issuedShares.text, publicPercentage.text, out var data, out var reason);
        if (businessName != null && !DailyIncomeController.ValidName(businessName.text))
        { valid = false; reason = "회사 이름 · 공백·태그 없이 2~20자"; }
        confirm.interactable = valid;
        error.text = reason;
        if (manager.economy.retailPlayerStart)
        {
            if (startBalance != null)
            { startBalance.text = manager.economy.retailStartingCash.ToString("N0") + "원"; }
            preview.text = "보유 주식 0주 · 개인 투자자로 시작\n\n봇 운영 리딩방에 가입해 실제 주문과 체결을 살펴보세요.\n매매 성과를 쌓으면 자신의 리딩방을 열 수 있습니다.";
            if (retailPreview != null)
            { retailPreview.text = "장 마감 회사 수익 +" + (DailyIncomeController.Instance?.Rules.companyIncome ?? 250000).ToString("N0") + "원"; }
            return;
        }
        preview.text = data.Shares == 0 ? "입력값을 확인하세요." : $"시작 단가 {data.Price:N0}원  ·  실제 시가총액 {data.MarketCap:N0}원\n공개 {data.PublicShares:N0}개  ·  본인 보유 {data.OwnShares:N0}개\n조달 현금 {data.Funding:N0}원\n최종 초기 현금 {data.FinalCash:N0}원\n거래소 초기 현금 {data.ExchangeCash:N0}원";
    }
```

수정본:

```csharp
public void Refresh()
    {
        var manager = StockManager.Instance;
        bool valid = manager.PreviewPlayer(companyName.text, issuedShares.text, publicPercentage.text, out var data, out var reason);
        if (businessName != null && !DailyIncomeController.ValidName(businessName.text))
        { valid = false; reason = "회사 이름 · 공백·태그 없이 2~20자"; }
        confirm.interactable = valid;
        GameLocalizedText.Set(error,reason);
        if (manager.economy.retailPlayerStart)
        {
            if (startBalance != null)
            { GameLocalizedText.Set(startBalance,manager.economy.retailStartingCash.ToString("N0") + "원"); }
            GameLocalizedText.Set(preview,"보유 주식 0주 · 개인 투자자로 시작\n\n봇 운영 리딩방에 가입해 실제 주문과 체결을 살펴보세요.\n매매 성과를 쌓으면 자신의 리딩방을 열 수 있습니다.");
            if (retailPreview != null)
            { GameLocalizedText.Set(retailPreview,"장 마감 회사 수익 +" + (DailyIncomeController.Instance?.Rules.companyIncome ?? 250000).ToString("N0") + "원"); }
            return;
        }
        GameLocalizedText.Set(preview,data.Shares == 0 ? "입력값을 확인하세요." : $"시작 단가 {data.Price:N0}원  ·  실제 시가총액 {data.MarketCap:N0}원\n공개 {data.PublicShares:N0}개  ·  본인 보유 {data.OwnShares:N0}개\n조달 현금 {data.Funding:N0}원\n최종 초기 현금 {data.FinalCash:N0}원\n거래소 초기 현금 {data.ExchangeCash:N0}원");
    }
```

#### Confirm

원본:

```csharp
public void Confirm()
    {
        if (businessName != null && (DailyIncomeController.Instance == null || !DailyIncomeController.Instance.NameCompany(businessName.text)))
        { error.text = "회사 이름을 확인하세요."; return; }
        if (!StockManager.Instance.ConfirmPlayer(companyName.text, issuedShares.text, publicPercentage.text, out string reason))
        {
            error.text = reason;
            return;
        }

        confirm.interactable = false;
        NpcLeadingRoom.Instance?.RequestJoin();
        companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false;
        error.text = "초기 자산 설정 중…";
    }
```

수정본:

```csharp
public void Confirm()
    {
        if (businessName != null && (DailyIncomeController.Instance == null || !DailyIncomeController.Instance.NameCompany(businessName.text)))
        { GameLocalizedText.Set(error,"회사 이름을 확인하세요."); return; }
        if (!StockManager.Instance.ConfirmPlayer(companyName.text, issuedShares.text, publicPercentage.text, out string reason))
        {
            GameLocalizedText.Set(error,reason);
            return;
        }

        confirm.interactable = false;
        if (!StockManager.Instance.economy.retailPlayerStart) { GameLocale.ProtectLiteral(companyName.text); }
        NpcLeadingRoom.Instance?.RequestJoin();
        companyName.interactable = issuedShares.interactable = publicPercentage.interactable = false;
        GameLocalizedText.Set(error,"초기 자산 설정 중…");
    }
```

### Assets/Scripts/ChartUI/ChartGuideOverlay.cs

#### Refresh

원본:

```csharp
public void Refresh(Stock stock)
    {
        bool hasData = chart.candles != null && chart.candles.Count > 0;
        if (gameObject.activeSelf != hasData)
        { gameObject.SetActive(hasData); }
        if (!hasData)
        { HideCursor(); return; }
        var scale = chart.PriceScale;
        bool changed = scale.Minimum != lastMinimum || scale.Maximum != lastMaximum || scale.Step != lastStep;
        if (changed)
        {
            // 큰 금액도 자동 축소하지 않고, 축의 폭을 필요한 만큼만 확보한다.
            string largest = scale.Maximum.ToString("N0", Numbers);
            float required = Mathf.Max(124f, ticks[0].GetPreferredValues(largest + "원").x + 22f);
            required = Mathf.Min(chart.ChartWidth * unitsX * .28f, required);
            if (Mathf.Abs(required - axisWidth) > .5f)
            {
                axisWidth = required;
                chart.ConfigurePlot(chart.PlotCapacity, axisWidth / unitsX, TimeBand / unitsY, chart.MaximumGridIntervals);
                chart.GenerateMesh();
            }
        }
        float width = chart.PlotWidth * unitsX, height = chart.chartHeight * unitsY;
        bool geometryChanged = width != plotWidth || height != plotHeight || volumeHeight != chart.VolumePlotHeight * unitsY;
        if (!changed && !geometryChanged)
        { RefreshTrades(stock, false); return; }
        plotWidth = width; plotHeight = height; volumeHeight = chart.VolumePlotHeight * unitsY;
        root.sizeDelta = new Vector2(chart.ChartWidth * unitsX, plotHeight);
        grid.rectTransform.sizeDelta = root.sizeDelta;
        hoverRoot.sizeDelta = crosshair.rectTransform.sizeDelta = root.sizeDelta;
        grid.SetGrid(plotWidth, plotHeight, scale.Intervals);
        for (int i = 0; i < ticks.Length; i++)
        {
            bool active = i <= scale.Intervals;
            if (ticks[i].gameObject.activeSelf != active)
            { ticks[i].gameObject.SetActive(active); }
            if (!active)
            { continue; }
            ticks[i].text = scale.Tick(i).ToString("N0", Numbers);
            float y = plotHeight * i / scale.Intervals;
            Place(ticks[i].rectTransform, plotWidth + 8f, Mathf.Clamp(y - LabelHeight * .5f, 0f, plotHeight - LabelHeight), axisWidth - 14f, LabelHeight);
        }
        lastMinimum = scale.Minimum; lastMaximum = scale.Maximum; lastStep = scale.Step;
        RefreshTrades(stock, true);
    }
```

수정본:

```csharp
public void Refresh(Stock stock)
    {
        bool hasData = chart.candles != null && chart.candles.Count > 0;
        if (gameObject.activeSelf != hasData)
        { gameObject.SetActive(hasData); }
        if (!hasData)
        { HideCursor(); return; }
        var scale = chart.PriceScale;
        bool changed = scale.Minimum != lastMinimum || scale.Maximum != lastMaximum || scale.Step != lastStep;
        if (changed)
        {
            // 큰 금액도 자동 축소하지 않고, 축의 폭을 필요한 만큼만 확보한다.
            string largest = scale.Maximum.ToString("N0", Numbers);
            float required = Mathf.Max(124f, ticks[0].GetPreferredValues(largest + "원").x + 22f);
            required = Mathf.Min(chart.ChartWidth * unitsX * .28f, required);
            if (Mathf.Abs(required - axisWidth) > .5f)
            {
                axisWidth = required;
                chart.ConfigurePlot(chart.PlotCapacity, axisWidth / unitsX, TimeBand / unitsY, chart.MaximumGridIntervals);
                chart.GenerateMesh();
            }
        }
        float width = chart.PlotWidth * unitsX, height = chart.chartHeight * unitsY;
        bool geometryChanged = width != plotWidth || height != plotHeight || volumeHeight != chart.VolumePlotHeight * unitsY;
        if (!changed && !geometryChanged)
        { RefreshTrades(stock, false); return; }
        plotWidth = width; plotHeight = height; volumeHeight = chart.VolumePlotHeight * unitsY;
        root.sizeDelta = new Vector2(chart.ChartWidth * unitsX, plotHeight);
        grid.rectTransform.sizeDelta = root.sizeDelta;
        hoverRoot.sizeDelta = crosshair.rectTransform.sizeDelta = root.sizeDelta;
        grid.SetGrid(plotWidth, plotHeight, scale.Intervals);
        for (int i = 0; i < ticks.Length; i++)
        {
            bool active = i <= scale.Intervals;
            if (ticks[i].gameObject.activeSelf != active)
            { ticks[i].gameObject.SetActive(active); }
            if (!active)
            { continue; }
            GameLocalizedText.Set(ticks[i],scale.Tick(i).ToString("N0", Numbers));
            float y = plotHeight * i / scale.Intervals;
            Place(ticks[i].rectTransform, plotWidth + 8f, Mathf.Clamp(y - LabelHeight * .5f, 0f, plotHeight - LabelHeight), axisWidth - 14f, LabelHeight);
        }
        lastMinimum = scale.Minimum; lastMaximum = scale.Maximum; lastStep = scale.Step;
        RefreshTrades(stock, true);
    }
```

#### SetText

원본:

```csharp
private static void SetText(TMP_Text target, string value)
    {
        if (target.text != value)
        { target.text = value; }
    }
```

수정본:

```csharp
private static void SetText(TMP_Text target, string value)
    {
        if (!GameLocalizedText.Matches(target,value))
        { GameLocalizedText.Set(target,value); }
    }
```

#### Label

원본:

```csharp
private static TextMeshProUGUI Label(string name, Transform parent, TMP_Text source, Color color)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = source.font; label.fontSharedMaterial = source.fontSharedMaterial;
        label.fontSize = FontSize; label.enableAutoSizing = false; label.color = color;
        label.text = "";
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Truncate;
        label.alignment = TextAlignmentOptions.MidlineRight; label.raycastTarget = false;
        return label;
    }
```

수정본:

```csharp
private static TextMeshProUGUI Label(string name, Transform parent, TMP_Text source, Color color)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = source.font; label.fontSharedMaterial = source.fontSharedMaterial;
        label.fontSize = FontSize; label.enableAutoSizing = false; label.color = color;
        GameLocalizedText.Set(label,"");
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Truncate;
        label.alignment = TextAlignmentOptions.MidlineRight; label.raycastTarget = false;
        return label;
    }
```

### Assets/Scripts/ChartUI/TradeHistoryItem.cs

#### Set

원본:

```csharp
public void Set(decimal price, decimal volume, bool isBuyInitiated)
    {
        _isBuyInitiated = isBuyInitiated;

        if (txtPrice != null)
        {
            txtPrice.text = PriceTickPolicy.FormatPrice(price);
            txtPrice.color = isBuyInitiated ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.3f, 0.3f);
        }

        if (txtVolume != null)
        {
            decimal totalAmount = price * volume;
            txtVolume.text = totalAmount.ToString("N0");
        }
    }
```

수정본:

```csharp
public void Set(decimal price, decimal volume, bool isBuyInitiated)
    {
        _isBuyInitiated = isBuyInitiated;

        if (txtPrice != null)
        {
            GameLocalizedText.Set(txtPrice,PriceTickPolicy.FormatPrice(price));
            txtPrice.color = isBuyInitiated ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.3f, 0.3f);
        }

        if (txtVolume != null)
        {
            decimal totalAmount = price * volume;
            GameLocalizedText.Set(txtVolume,totalAmount.ToString("N0"));
        }
    }
```

### Assets/Scripts/Interaction/InteractionManager.cs

#### ShowHint

원본:

```csharp
void ShowHint(Interactable interactable)
    {
        if (hintRoot != null)
            hintRoot.SetActive(true);

        if (keyText != null)
            keyText.text = $"[{GameKeyBindings.Interact}]";

        if (interactionText != null)
            interactionText.text = interactable != null ? interactable.interactionText : "";
    }
```

수정본:

```csharp
void ShowHint(Interactable interactable)
    {
        if (hintRoot != null)
            hintRoot.SetActive(true);

        if (keyText != null)
            GameLocalizedText.Set(keyText,$"[{GameKeyBindings.Interact}]");

        if (interactionText != null)
            GameLocalizedText.Set(interactionText,interactable != null ? interactable.interactionText : "");
    }
```

#### HideHint

원본:

```csharp
void HideHint()
    {
        if (hintRoot != null)
            hintRoot.SetActive(false);

        if (keyText != null)
            keyText.text = "";

        if (interactionText != null)
            interactionText.text = "";
    }
```

수정본:

```csharp
void HideHint()
    {
        if (hintRoot != null)
            hintRoot.SetActive(false);

        if (keyText != null)
            GameLocalizedText.Set(keyText,"");

        if (interactionText != null)
            GameLocalizedText.Set(interactionText,"");
    }
```

### Assets/Scripts/MarketDay/TabletGroupChatUI.cs

#### SelectRoom

원본:

```csharp
public void SelectRoom(int room)
    {
        moderationMenu?.Close();
        SelectedRoom = Mathf.Clamp(room, 0, InvestorSociety.MaximumRoomId);
        if (SelectedRoom > InvestorSociety.AvailablePlayerRoomCount && SelectedRoom <= InvestorSociety.PlayerRoomCount) { SelectedRoom = 0; }
        selectedBot = 0;
        if (profile != null)
        { profile.text = "메시지를 눌러 참여자의 경험과 심리를 확인하세요."; }
        if (visualProfile != null) { visualProfile.Clear(); }
        measuredHeights.Clear(); displayedMessages.Clear(); retainedSequences.Clear(); rowOffsets.Clear(); rowHeights.Clear();
        foreach (var row in rows) { row.Sequence = -1; }
        version = -1; readSequence = oldestSequence = 0; lastUnread = -1; lastParticipantCount = -1;
        firstVisible = -1; followNewest = true; dirty = true;
        scroll.StopMovement(); scrollInput?.StopFollowingGesture();
        ShowChat();
    }
```

수정본:

```csharp
public void SelectRoom(int room)
    {
        moderationMenu?.Close();
        SelectedRoom = Mathf.Clamp(room, 0, InvestorSociety.MaximumRoomId);
        if (SelectedRoom > InvestorSociety.AvailablePlayerRoomCount && SelectedRoom <= InvestorSociety.PlayerRoomCount) { SelectedRoom = 0; }
        selectedBot = 0;
        if (profile != null)
        { GameLocalizedText.Set(profile,"메시지를 눌러 참여자의 경험과 심리를 확인하세요."); }
        if (visualProfile != null) { visualProfile.Clear(); }
        measuredHeights.Clear(); displayedMessages.Clear(); retainedSequences.Clear(); rowOffsets.Clear(); rowHeights.Clear();
        foreach (var row in rows) { row.Sequence = -1; }
        version = -1; readSequence = oldestSequence = 0; lastUnread = -1; lastParticipantCount = -1;
        firstVisible = -1; followNewest = true; dirty = true;
        scroll.StopMovement(); scrollInput?.StopFollowingGesture();
        ShowChat();
    }
```

#### Start

원본:

```csharp
void Start()
    {
        day = MarketDayController.Instance;
        if (chatTab != null)
        { chatTab.onClick.AddListener(ShowChat); }
        if (informationTab != null)
        { informationTab.onClick.AddListener(ShowInformation); }
        newestButton.onClick.AddListener(ShowNewest);
        scroll.onValueChanged.AddListener(OnScroll);
        ConfigureScrolling();
        informationScroll = informationPanel == null ? null : informationPanel.GetComponentInChildren<ScrollRect>(true);
        messageMeasure = rowTemplate.Find("Text :: Message").GetComponent<TextMeshProUGUI>();
        messageMeasure.richText = true;
        BuildTypingIndicator();
        nicknameMeasure = rowTemplate.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>();
        moderationMenu = gameObject.AddComponent<ChatSocialMenuUnity>(); moderationMenu.Initialize(this, nicknameMeasure.font);
        int visible = Mathf.Clamp(Mathf.CeilToInt(scroll.viewport.rect.height / rowHeight) + 2, 1, rowPoolLimit);
        for (int i = 0; i < visible; i++)
        {
            var rect = Instantiate(rowTemplate, scroll.content);
            rect.name = "Button :: Chat Message Row " + (i + 1);
            rect.gameObject.SetActive(false);
            var row = new Row
            {
                Rect = rect,
                Text = rect.Find("Text :: Message").GetComponent<TextMeshProUGUI>(),
                Nickname = rect.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>(),
                Time = rect.Find("Text :: Timestamp").GetComponent<TextMeshProUGUI>(),
                Bubble = rect.Find("Image :: Bubble").GetComponent<RectTransform>(),
                Avatar = rect.Find("Image :: Avatar").GetComponent<Image>(),
                Gauge = rect.Find("Image :: Stress").GetComponent<Image>(),
                StressRing = rect.Find("Image :: Stress").GetComponentInChildren<TerminalGlyph>(true)
            };
            row.Click = rect.GetComponent<Button>();
            row.Click.onClick.AddListener(() => { SelectProfile(row.BotId); ChatSocialDebugUnity.ShowMessage(row.SocialMessage); });
            row.Nickname.raycastTarget = true;
            var nicknameButton = row.Nickname.gameObject.GetComponent<Button>() ?? row.Nickname.gameObject.AddComponent<Button>();
            nicknameButton.targetGraphic = row.Nickname;
            nicknameButton.onClick.AddListener(() => { SelectProfile(row.BotId); moderationMenu.Open(row.BotId, row.Nickname.rectTransform); ChatSocialDebugUnity.ShowBot(row.BotId); });
            row.AngerDot = Instantiate(row.Nickname, rect);
            row.AngerDot.name = "Text :: Anger Indicator"; row.AngerDot.text = "●";
            row.AngerDot.raycastTarget = false; row.AngerDot.enableAutoSizing = false;
            row.AngerDot.fontSize = 18f * layoutScale; row.AngerDot.alignment = TextAlignmentOptions.Center;
            var dot = row.AngerDot.rectTransform;
            dot.anchorMin = dot.anchorMax = row.Nickname.rectTransform.anchorMin;
            dot.pivot = row.Nickname.rectTransform.pivot;
            dot.anchoredPosition = row.Nickname.rectTransform.anchoredPosition + new Vector2(-24f * layoutScale, 0);
            dot.sizeDelta = new Vector2(20f, 30f) * layoutScale;
            row.AngerDot.gameObject.SetActive(false);
            BuildNoticeVisual(row);
            row.Text.raycastTarget = true;
            var stockLinks = row.Text.gameObject.AddComponent<ChatStockLinkUI>();
            stockLinks.Bind(row.Text, SelectMentionedStock, () => row.Click.onClick.Invoke());
            rows.Add(row);
        }

        rowTemplate.gameObject.SetActive(false);
        if (monitorChat)
        { ShowChat(); }
        else
        { ShowInformation(); }
    }
```

수정본:

```csharp
void Start()
    {
        day = MarketDayController.Instance;
        if (chatTab != null)
        { chatTab.onClick.AddListener(ShowChat); }
        if (informationTab != null)
        { informationTab.onClick.AddListener(ShowInformation); }
        newestButton.onClick.AddListener(ShowNewest);
        scroll.onValueChanged.AddListener(OnScroll);
        ConfigureScrolling();
        informationScroll = informationPanel == null ? null : informationPanel.GetComponentInChildren<ScrollRect>(true);
        messageMeasure = rowTemplate.Find("Text :: Message").GetComponent<TextMeshProUGUI>();
        messageMeasure.richText = true;
        BuildTypingIndicator();
        nicknameMeasure = rowTemplate.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>();
        moderationMenu = gameObject.AddComponent<ChatSocialMenuUnity>(); moderationMenu.Initialize(this, nicknameMeasure.font);
        int visible = Mathf.Clamp(Mathf.CeilToInt(scroll.viewport.rect.height / rowHeight) + 2, 1, rowPoolLimit);
        for (int i = 0; i < visible; i++)
        {
            var rect = Instantiate(rowTemplate, scroll.content);
            rect.name = "Button :: Chat Message Row " + (i + 1);
            rect.gameObject.SetActive(false);
            var row = new Row
            {
                Rect = rect,
                Text = rect.Find("Text :: Message").GetComponent<TextMeshProUGUI>(),
                Nickname = rect.Find("Text :: Nickname").GetComponent<TextMeshProUGUI>(),
                Time = rect.Find("Text :: Timestamp").GetComponent<TextMeshProUGUI>(),
                Bubble = rect.Find("Image :: Bubble").GetComponent<RectTransform>(),
                Avatar = rect.Find("Image :: Avatar").GetComponent<Image>(),
                Gauge = rect.Find("Image :: Stress").GetComponent<Image>(),
                StressRing = rect.Find("Image :: Stress").GetComponentInChildren<TerminalGlyph>(true)
            };
            row.Click = rect.GetComponent<Button>();
            row.Click.onClick.AddListener(() => { SelectProfile(row.BotId); ChatSocialDebugUnity.ShowMessage(row.SocialMessage); });
            row.Nickname.raycastTarget = true;
            var nicknameButton = row.Nickname.gameObject.GetComponent<Button>() ?? row.Nickname.gameObject.AddComponent<Button>();
            nicknameButton.targetGraphic = row.Nickname;
            nicknameButton.onClick.AddListener(() => { SelectProfile(row.BotId); moderationMenu.Open(row.BotId, row.Nickname.rectTransform); ChatSocialDebugUnity.ShowBot(row.BotId); });
            row.AngerDot = Instantiate(row.Nickname, rect);
            row.AngerDot.name = "Text :: Anger Indicator"; GameLocalizedText.Set(row.AngerDot,"●");
            row.AngerDot.raycastTarget = false; row.AngerDot.enableAutoSizing = false;
            row.AngerDot.fontSize = 18f * layoutScale; row.AngerDot.alignment = TextAlignmentOptions.Center;
            var dot = row.AngerDot.rectTransform;
            dot.anchorMin = dot.anchorMax = row.Nickname.rectTransform.anchorMin;
            dot.pivot = row.Nickname.rectTransform.pivot;
            dot.anchoredPosition = row.Nickname.rectTransform.anchoredPosition + new Vector2(-24f * layoutScale, 0);
            dot.sizeDelta = new Vector2(20f, 30f) * layoutScale;
            row.AngerDot.gameObject.SetActive(false);
            BuildNoticeVisual(row);
            row.Text.raycastTarget = true;
            var stockLinks = row.Text.gameObject.AddComponent<ChatStockLinkUI>();
            stockLinks.Bind(row.Text, SelectMentionedStock, () => row.Click.onClick.Invoke());
            rows.Add(row);
        }

        rowTemplate.gameObject.SetActive(false);
        if (monitorChat)
        { ShowChat(); }
        else
        { ShowInformation(); }
    }
```

#### EnsureMentionRoster

원본:

```csharp
private void EnsureMentionRoster()
    {
        var society = InvestorSociety.Instance;
        var stocks = StockManager.Instance;
        int count = society == null ? 0 : society.SocialBots.Count;
        if (mentions != null && !mentionRosterDirty && mentionSociety == society && mentionBotCount == count && mentionStockManager == stocks)
        { return; }
        var names = new List<string> { "방장" };
        if (society != null)
        { foreach (var bot in society.SocialBots.Values) { names.Add(bot.Social.Nickname); } }
        var aliases = new List<KeyValuePair<string, string>>();
        if (stocks != null)
        {
            foreach (var stock in stocks.stocks)
            {
                if (stock == null || !stock.IsListed || stock.IsDelisted) { continue; }
                aliases.Add(new KeyValuePair<string, string>(stock.stockName, stock.symbol));
                aliases.Add(new KeyValuePair<string, string>(stock.symbol, stock.symbol));
            }
        }
        mentions = new ChatSocialMentions(names, aliases); mentionSociety = society; mentionBotCount = count;
        mentionStockManager = stocks; mentionRosterDirty = false;
        displayedMessages.Clear(); measuredHeights.Clear(); version = -1; dirty = true;
        foreach (var row in rows) { row.Sequence = -1; }
    }
```

수정본:

```csharp
private void EnsureMentionRoster()
    {
        var society = InvestorSociety.Instance;
        var stocks = StockManager.Instance;
        int count = society == null ? 0 : society.SocialBots.Count;
        if (mentions != null && !mentionRosterDirty && mentionSociety == society && mentionBotCount == count && mentionStockManager == stocks)
        { return; }
        var names = new List<string> { "방장", GameLocale.Text("방장") };
        if (society != null)
        { foreach (var bot in society.SocialBots.Values) { names.Add(bot.Social.Nickname); GameLocale.ProtectLiteral(bot.Social.Nickname); } }
        var aliases = new List<KeyValuePair<string, string>>();
        if (stocks != null)
        {
            foreach (var stock in stocks.stocks)
            {
                if (stock == null || !stock.IsListed || stock.IsDelisted) { continue; }
                aliases.Add(new KeyValuePair<string, string>(stock.stockName, stock.symbol));
                aliases.Add(new KeyValuePair<string, string>(GameLocale.Text(stock.stockName), stock.symbol));
                aliases.Add(new KeyValuePair<string, string>(stock.symbol, stock.symbol));
            }
        }
        mentions = new ChatSocialMentions(names, aliases); mentionSociety = society; mentionBotCount = count;
        mentionStockManager = stocks; mentionRosterDirty = false;
        displayedMessages.Clear(); measuredHeights.Clear(); version = -1; dirty = true;
        foreach (var row in rows) { row.Sequence = -1; }
    }
```

#### BuildTypingIndicator

원본:

```csharp
private void BuildTypingIndicator()
    {
        float height = 44f * layoutScale;
        // Reserve a fixed footer once; typing never resizes the pooled message content.
        scroll.viewport.offsetMin += new Vector2(0, height);
        ((RectTransform)newestButton.transform).anchoredPosition += new Vector2(0, height);
        var item = new GameObject("Text :: Chat Typing", typeof(RectTransform), typeof(TextMeshProUGUI));
        item.layer = scroll.gameObject.layer; item.transform.SetParent(scroll.transform, false);
        typingLabel = item.GetComponent<TextMeshProUGUI>();
        typingLabel.font = messageMeasure.font; typingLabel.fontSize = 22f * layoutScale;
        typingLabel.fontStyle = FontStyles.Normal; typingLabel.color = new Color(.60f, .66f, .71f, 1f);
        typingLabel.raycastTarget = false; typingLabel.richText = false; typingLabel.enableAutoSizing = false;
        typingLabel.textWrappingMode = TextWrappingModes.NoWrap; typingLabel.overflowMode = TextOverflowModes.Ellipsis;
        typingLabel.alignment = TextAlignmentOptions.MidlineLeft;
        var rect = typingLabel.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 0);
        rect.offsetMin = new Vector2(18f * layoutScale, 0); rect.offsetMax = new Vector2(-18f * layoutScale, height);
        typingLabel.text = "";
    }
```

수정본:

```csharp
private void BuildTypingIndicator()
    {
        float height = 44f * layoutScale;
        // Reserve a fixed footer once; typing never resizes the pooled message content.
        scroll.viewport.offsetMin += new Vector2(0, height);
        ((RectTransform)newestButton.transform).anchoredPosition += new Vector2(0, height);
        var item = new GameObject("Text :: Chat Typing", typeof(RectTransform), typeof(TextMeshProUGUI));
        item.layer = scroll.gameObject.layer; item.transform.SetParent(scroll.transform, false);
        typingLabel = item.GetComponent<TextMeshProUGUI>();
        typingLabel.font = messageMeasure.font; typingLabel.fontSize = 22f * layoutScale;
        typingLabel.fontStyle = FontStyles.Normal; typingLabel.color = new Color(.60f, .66f, .71f, 1f);
        typingLabel.raycastTarget = false; typingLabel.richText = false; typingLabel.enableAutoSizing = false;
        typingLabel.textWrappingMode = TextWrappingModes.NoWrap; typingLabel.overflowMode = TextOverflowModes.Ellipsis;
        typingLabel.alignment = TextAlignmentOptions.MidlineLeft;
        var rect = typingLabel.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 0);
        rect.offsetMin = new Vector2(18f * layoutScale, 0); rect.offsetMax = new Vector2(-18f * layoutScale, height);
        GameLocalizedText.Set(typingLabel,"");
    }
```

#### DisplayMessage

원본:

```csharp
private string DisplayMessage(BotChatMessage message)
    {
        if (!displayedMessages.TryGetValue(message.Sequence, out string text))
        { text = message.IsRoomNotice ? ChatSocialMentions.Literal(message.Text) : mentions.Format(message.Text); displayedMessages.Add(message.Sequence, text); }
        return text;
    }
```

수정본:

```csharp
private string DisplayMessage(BotChatMessage message)
    {
        if (!displayedMessages.TryGetValue(message.Sequence, out string text))
        { string localized = LocalizedChat.Text(message); text = message.IsRoomNotice ? ChatSocialMentions.Literal(localized) : mentions.Format(localized); displayedMessages.Add(message.Sequence, text); }
        return text;
    }
```

#### LayoutNotice

원본:

```csharp
private void LayoutNotice(Row row, BotChatMessage message)
    {
        DeviceTerminalUI.Set(row.NoticeText, DisplayMessage(message));
        float available = Mathf.Max(1f, row.Rect.rect.width - 40f * layoutScale);
        float width = Mathf.Min(available, row.NoticeText.GetPreferredValues(row.NoticeText.text).x + 36f * layoutScale);
        float height = 40f * layoutScale;
        row.NoticeBackground.rectTransform.sizeDelta = new Vector2(width, height);
        row.NoticeText.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, width - 24f * layoutScale), height);
    }
```

수정본:

```csharp
private void LayoutNotice(Row row, BotChatMessage message)
    {
        GameLocalizedText.Set(row.NoticeText, DisplayMessage(message),true);
        float available = Mathf.Max(1f, row.Rect.rect.width - 40f * layoutScale);
        float width = Mathf.Min(available, row.NoticeText.GetPreferredValues(row.NoticeText.text).x + 36f * layoutScale);
        float height = 40f * layoutScale;
        row.NoticeBackground.rectTransform.sizeDelta = new Vector2(width, height);
        row.NoticeText.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, width - 24f * layoutScale), height);
    }
```

#### Update

원본:

```csharp
void Update()
    {
        if (day == null)
        { return; }
        EnsureMentionRoster();
        RefreshTypingIndicator();

        int participantCount = SelectedRoom == 0 ? day.Bots.Count : Room == null ? 0 : Room.Members.Count;
        if (SelectedRoom == NpcLeadingRoom.RoomId && NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined)
        { participantCount++; }
        if (participants != null && lastParticipantCount != participantCount)
        {
            lastParticipantCount = participantCount;
            participants.text = lastParticipantCount.ToString("N0");
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && hudStatus.isActiveAndEnabled && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            hudStatus.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}";
        }

        if (!monitorChat && !TabletViewController.IsTabletHeld)
        {
            wasHeld = false;
            return;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            dirty = true;
            lastSecond = -1;
        }

        using (UiMarker.Auto())
        {
            int second = Mathf.CeilToInt(day.RemainingSeconds);
            if (second != lastSecond || dirty || minute != lastMinute || day.Day != lastDay)
            {
                lastSecond = second;
                lastMinute = minute;
                lastDay = day.Day;
                string phase = PhaseText(day.Phase);
                status.text = $"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}";
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - 154f * layoutScale);
            if (version != MessageVersion || !Mathf.Approximately(width, measuredWidth))
            {
                RebuildMessageLayout(width);
            }

            int start = FindFirstVisible(scroll.content.anchoredPosition.y);
            if (firstVisible != start)
            {
                dirty = true;
                firstVisible = start;
            }

            if (dirty)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int index = start + i;
                    bool valid = index < MessageCount;
                    row.Rect.gameObject.SetActive(valid);
                    if (!valid)
                    { continue; }

                    var message = Message(index);
                    row.Rect.anchoredPosition = new Vector2(0f, -rowOffsets[index]);
                    row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[index]);
                    if (row.Sequence != message.Sequence)
                    {
                        row.Sequence = message.Sequence;
                        row.BotId = message.BotId;
                        row.SocialMessage = message.Social;
                        SetNoticeMode(row, message.IsRoomNotice);
                        row.Nickname.text = message.Nickname;
                        row.Time.text = $"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}";
                        row.Text.text = DisplayMessage(message);
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }
                    if (row.IsNotice)
                    { LayoutNotice(row, message); continue; }
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(row.Nickname.text, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader);
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(row.Text.text, width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            if (Time.time >= nextAngerRefresh)
            {
                nextAngerRefresh = Time.time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null) { DeviceTerminalUI.Set(row.Nickname, name + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : "")); }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(Time.time * 5f) * .5f + .5f) : 1f;
                    row.AngerDot.color = color;
                }
            }
            if (selectedBot > 0 && second != lastProfileSecond)
            {
                lastProfileSecond = second;
                var bot = day.FindBot(selectedBot);
                if (visualProfile != null)
                { visualProfile.Bind(bot == null ? null : bot.Social); }
                else if (bot == null)
                {
                    profile.text = "참여자가 퇴장했습니다.";
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    profile.text = $"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}";
                }
            }
        }
    }
```

수정본:

```csharp
void Update()
    {
        if (localeRevision != GameLocale.Revision)
        { localeRevision = GameLocale.Revision; mentionRosterDirty = true; }
        if (day == null)
        { return; }
        EnsureMentionRoster();
        RefreshTypingIndicator();

        int participantCount = SelectedRoom == 0 ? day.Bots.Count : Room == null ? 0 : Room.Members.Count;
        if (SelectedRoom == NpcLeadingRoom.RoomId && NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined)
        { participantCount++; }
        if (participants != null && lastParticipantCount != participantCount)
        {
            lastParticipantCount = participantCount;
            GameLocalizedText.Set(participants,lastParticipantCount.ToString("N0"));
        }

        int minute = day.GameMinute;
        int hudSecond = Mathf.CeilToInt(day.RemainingSeconds);
        if (hudStatus != null && hudStatus.isActiveAndEnabled && (hudSecond != lastHudSecond || day.Phase != lastHudPhase || minute != lastHudMinute || day.Day != lastHudDay))
        {
            lastHudSecond = hudSecond;
            lastHudMinute = minute;
            lastHudDay = day.Day;
            lastHudPhase = day.Phase;
            GameLocalizedText.Set(hudStatus,$"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {PhaseText(day.Phase)} · {hudSecond / 60:D2}:{hudSecond % 60:D2}");
        }

        if (!monitorChat && !TabletViewController.IsTabletHeld)
        {
            wasHeld = false;
            return;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            dirty = true;
            lastSecond = -1;
        }

        using (UiMarker.Auto())
        {
            int second = Mathf.CeilToInt(day.RemainingSeconds);
            if (second != lastSecond || dirty || minute != lastMinute || day.Day != lastDay)
            {
                lastSecond = second;
                lastMinute = minute;
                lastDay = day.Day;
                string phase = PhaseText(day.Phase);
                GameLocalizedText.Set(status,$"{day.Day}일 · {day.GameMinute / 60:D2}:{day.GameMinute % 60:D2} · {phase} · {second / 60:D2}:{second % 60:D2}");
            }

            if (!chatSelected)
            {
                UpdateUnread();
                return;
            }

            float width = Mathf.Max(1f, scroll.viewport.rect.width - 154f * layoutScale);
            if (version != MessageVersion || !Mathf.Approximately(width, measuredWidth))
            {
                RebuildMessageLayout(width);
            }

            int start = FindFirstVisible(scroll.content.anchoredPosition.y);
            if (firstVisible != start)
            {
                dirty = true;
                firstVisible = start;
            }

            if (dirty)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int index = start + i;
                    bool valid = index < MessageCount;
                    row.Rect.gameObject.SetActive(valid);
                    if (!valid)
                    { continue; }

                    var message = Message(index);
                    row.Rect.anchoredPosition = new Vector2(0f, -rowOffsets[index]);
                    row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeights[index]);
                    if (row.Sequence != message.Sequence)
                    {
                        row.Sequence = message.Sequence;
                        row.BotId = message.BotId;
                        row.SocialMessage = message.Social;
                        SetNoticeMode(row, message.IsRoomNotice);
                        GameLocalizedText.Set(row.Nickname,message.Nickname,true);
                        GameLocalizedText.Set(row.Time,$"{message.Day}일 {message.Minute / 60:D2}:{message.Minute % 60:D2}");
                        GameLocalizedText.Set(row.Text,DisplayMessage(message),true);
                        int avatarIndex = (int)((uint)message.BotId % 3u);
                        row.Avatar.sprite = avatarIndex == 0 ? avatarOrange : avatarIndex == 1 ? avatarBlue : avatarPink;
                        if (row.StressRing != null) { row.StressRing.SetValue(message.Stress); }
                        else
                        {
                            row.Gauge.fillAmount = message.Stress / 100f;
                            row.Gauge.color = TerminalGlyph.BandColor(message.Stress);
                        }
                    }
                    if (row.IsNotice)
                    { LayoutNotice(row, message); continue; }
                    float nicknameHeight = Mathf.Max(30f * layoutScale, row.Nickname.GetPreferredValues(GameLocale.Text(row.Nickname.text), nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y);
                    float extraHeader = nicknameHeight - 30f * layoutScale;
                    row.Nickname.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nicknameHeight);
                    row.Bubble.anchoredPosition = new Vector2(104f * layoutScale, -38f * layoutScale - extraHeader);
                    row.Text.rectTransform.anchoredPosition = new Vector2(124f * layoutScale, -48f * layoutScale - extraHeader);
                    float bubbleHeight = Mathf.Max(52f * layoutScale, rowHeights[index] - 48f * layoutScale - extraHeader);
                    float bubbleWidth = Mathf.Clamp(row.Text.GetPreferredValues(GameLocale.Text(row.Text.text), width, Mathf.Infinity).x + 40f * layoutScale, Mathf.Min(220f * layoutScale, width + 40f * layoutScale), width + 40f * layoutScale);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    row.Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 40f * layoutScale);
                    row.Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight - 20f * layoutScale);
                }

                dirty = false;
            }

            if (followNewest)
            {
                readSequence = MessageVersion;
            }

            UpdateUnread();
            if (Time.time >= nextAngerRefresh)
            {
                nextAngerRefresh = Time.time + .25f;
                foreach (var row in rows)
                {
                    if (!row.Rect.gameObject.activeSelf || row.IsNotice) { continue; }
                    var mind = day.FindBot(row.BotId)?.Social.Psychology;
                    var name = day.FindBot(row.BotId)?.Social.Nickname;
                    if (name != null) { DeviceTerminalUI.Set(row.Nickname, name + (mind.HasFomo ? " <size=70%><color=#E6944E>FOMO</color></size>" : "")); }
                    int angerStage = mind == null ? 0 : InvestorPsychology.Stage(mind.MaximumAnger(out _));
                    row.AngerDot.gameObject.SetActive(angerStage >= 2);
                    if (angerStage < 2) { continue; }
                    var color = angerStage == 2 ? new Color(1f, .58f, .22f) : new Color(.94f, .35f, .39f);
                    color.a = angerStage == 4 ? .4f + .6f * (Mathf.Sin(Time.time * 5f) * .5f + .5f) : 1f;
                    row.AngerDot.color = color;
                }
            }
            if (selectedBot > 0 && second != lastProfileSecond)
            {
                lastProfileSecond = second;
                var bot = day.FindBot(selectedBot);
                if (visualProfile != null)
                { visualProfile.Bind(bot == null ? null : bot.Social); }
                else if (bot == null)
                {
                    GameLocalizedText.Set(profile,"참여자가 퇴장했습니다.");
                }
                else
                {
                    var state = bot.Social;
                    var mind = state.Psychology;
                    GameLocalizedText.Set(profile,$"{state.Nickname} · {state.Profile} · {mind.QuadrantLabel}" + (mind.HasFomo ? " · FOMO" : "") + $"\n탐욕 {mind.Greed:0}  공포 {mind.Fear:0}\n신뢰 {mind.Trust:0}  배신감 {mind.Betrayal:0} · 직접 경험 {mind.Wins}승 / {mind.Losses}패\n스트레스 {state.Stress:0} · 위험 {state.Risk:F1} · 기간 {state.Horizon:F1} · 추격 {state.Chase:F1}\n민감 {state.Sensitivity:F1} · 회복 {state.Recovery:F1} · {state.Tone}");
                }
            }
        }
    }
```

#### RebuildMessageLayout

원본:

```csharp
private void RebuildMessageLayout(float width)
    {
        layoutFrame = Time.frameCount;
        float y = scroll.content.anchoredPosition.y;
        long oldest = MessageCount > 0 ? Message(0).Sequence : 0;
        long removed = oldestSequence > 0 ? System.Math.Max(0, oldest - oldestSequence) : 0;
        if (!followNewest && removed > 0 && rowOffsets.Count > 0)
        {
            int removedRows = (int)System.Math.Min(removed, rowOffsets.Count);
            float removedHeight = removedRows < rowOffsets.Count ? rowOffsets[removedRows] : rowOffsets[rowOffsets.Count - 1] + rowHeights[rowHeights.Count - 1];
            y = Mathf.Max(0f, y - removedHeight);
        }

        if (!Mathf.Approximately(width, measuredWidth))
        {
            measuredHeights.Clear();
            measuredWidth = width;
        }

        rowOffsets.Clear();
        rowHeights.Clear();
        retainedSequences.Clear();
        float total = 0f;
        for (int i = 0; i < MessageCount; i++)
        {
            var message = Message(i);
            retainedSequences.Add(message.Sequence);
            if (!measuredHeights.TryGetValue(message.Sequence, out float height))
            {
                float extraHeader = Mathf.Max(0f, nicknameMeasure.GetPreferredValues(message.Nickname, nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y - 30f * layoutScale);
                height = message.IsRoomNotice ? Mathf.Max(rowHeight, 52f * layoutScale) : Mathf.Max(rowHeight, messageMeasure.GetPreferredValues(DisplayMessage(message), width, Mathf.Infinity).y + 68f * layoutScale) + extraHeader;
                measuredHeights.Add(message.Sequence, height);
            }

            rowOffsets.Add(total);
            rowHeights.Add(height);
            total += height;
        }

        expiredSequences.Clear();
        foreach (var sequence in displayedMessages.Keys)
        {
            if (!retainedSequences.Contains(sequence))
            {
                expiredSequences.Add(sequence);
            }
        }

        foreach (var sequence in expiredSequences)
        {
            measuredHeights.Remove(sequence);
            displayedMessages.Remove(sequence);
        }

        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, total);
        if (followNewest)
        {
            SnapToNewest();
        }
        else
        {
            scroll.content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, Mathf.Max(0f, total - scroll.viewport.rect.height)));
        }

        oldestSequence = oldest;
        version = MessageVersion;
        dirty = true;
    }
```

수정본:

```csharp
private void RebuildMessageLayout(float width)
    {
        layoutFrame = Time.frameCount;
        float y = scroll.content.anchoredPosition.y;
        long oldest = MessageCount > 0 ? Message(0).Sequence : 0;
        long removed = oldestSequence > 0 ? System.Math.Max(0, oldest - oldestSequence) : 0;
        if (!followNewest && removed > 0 && rowOffsets.Count > 0)
        {
            int removedRows = (int)System.Math.Min(removed, rowOffsets.Count);
            float removedHeight = removedRows < rowOffsets.Count ? rowOffsets[removedRows] : rowOffsets[rowOffsets.Count - 1] + rowHeights[rowHeights.Count - 1];
            y = Mathf.Max(0f, y - removedHeight);
        }

        if (!Mathf.Approximately(width, measuredWidth))
        {
            measuredHeights.Clear();
            measuredWidth = width;
        }

        rowOffsets.Clear();
        rowHeights.Clear();
        retainedSequences.Clear();
        float total = 0f;
        for (int i = 0; i < MessageCount; i++)
        {
            var message = Message(i);
            retainedSequences.Add(message.Sequence);
            if (!measuredHeights.TryGetValue(message.Sequence, out float height))
            {
                float extraHeader = Mathf.Max(0f, nicknameMeasure.GetPreferredValues(GameLocale.Text(message.Nickname), nicknameMeasure.rectTransform.rect.width, Mathf.Infinity).y - 30f * layoutScale);
                height = message.IsRoomNotice ? Mathf.Max(rowHeight, 52f * layoutScale) : Mathf.Max(rowHeight, messageMeasure.GetPreferredValues(DisplayMessage(message), width, Mathf.Infinity).y + 68f * layoutScale) + extraHeader;
                measuredHeights.Add(message.Sequence, height);
            }

            rowOffsets.Add(total);
            rowHeights.Add(height);
            total += height;
        }

        expiredSequences.Clear();
        foreach (var sequence in displayedMessages.Keys)
        {
            if (!retainedSequences.Contains(sequence))
            {
                expiredSequences.Add(sequence);
            }
        }

        foreach (var sequence in expiredSequences)
        {
            measuredHeights.Remove(sequence);
            displayedMessages.Remove(sequence);
        }

        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, total);
        if (followNewest)
        {
            SnapToNewest();
        }
        else
        {
            scroll.content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, Mathf.Max(0f, total - scroll.viewport.rect.height)));
        }

        oldestSequence = oldest;
        version = MessageVersion;
        dirty = true;
    }
```

#### UpdateUnread

원본:

```csharp
private void UpdateUnread()
    {
        int count = (int)System.Math.Min(MessageCount, MessageVersion - readSequence);
        if (count != lastUnread)
        {
            lastUnread = count;
            unread.text = SelectedRoom > 0 ? Room.DisplayName : monitorChat ? "시장 라운지" : count > 0 ? $"새 메시지 {count}" : "시장 라운지";
        }
    }
```

수정본:

```csharp
private void UpdateUnread()
    {
        int count = (int)System.Math.Min(MessageCount, MessageVersion - readSequence);
        if (count != lastUnread)
        {
            lastUnread = count;
            GameLocalizedText.Set(unread,SelectedRoom > 0 ? Room.DisplayName : monitorChat ? "시장 라운지" : count > 0 ? $"새 메시지 {count}" : "시장 라운지");
        }
    }
```

### Assets/Scripts/News/CompanyNewsAudience.cs

#### Published

원본:

```csharp
private void Published(NewsArticle article)
    {
        var stock = Find(article.symbol); if (stock == null) { return; }
        if (notices.Count == 16) { notices.RemoveAt(0); }
        notices.Add(new Notice { article = article, stock = stock });
        if (publicResponses.Count >= 40)
        {
            long oldest = long.MaxValue; foreach (long key in publicResponses.Keys) { oldest = Math.Min(oldest, key); }
            publicResponses.Remove(oldest);
        }
        publicResponses[article.id] = new List<string>(3);
    }
```

수정본:

```csharp
private void Published(NewsArticle article)
    {
        var stock = Find(article.symbol); if (stock == null) { return; }
        if (notices.Count == 16) { notices.RemoveAt(0); }
        notices.Add(new Notice { article = article, stock = stock });
        if (publicResponses.Count >= 40)
        {
            long oldest = long.MaxValue; foreach (long key in publicResponses.Keys) { oldest = Math.Min(oldest, key); }
            publicResponses.Remove(oldest);
        }
        publicResponses[article.id] = new List<Response>(3);
    }
```

#### Spoken

원본:

```csharp
private void Spoken(SocialMessage message)
    {
        if (message.roomId != 0 || message.facts == null || !publicResponses.TryGetValue(message.facts.companyNewsId, out var responses)) { return; }
        string name = MarketDayController.Instance?.FindBot(message.senderId)?.Social.Nickname ?? "참여자";
        if (responses.Count == 3) { responses.RemoveAt(0); }
        responses.Add(name + "  " + message.text); Version++;
    }
```

수정본:

```csharp
private void Spoken(SocialMessage message)
    {
        if (message.roomId != 0 || message.facts == null || !publicResponses.TryGetValue(message.facts.companyNewsId, out var responses)) { return; }
        string name = MarketDayController.Instance?.FindBot(message.senderId)?.Social.Nickname ?? "참여자";
        if (responses.Count == 3) { responses.RemoveAt(0); }
        GameLocale.ProtectLiteral(name);
        responses.Add(new Response { name = name, message = message }); Version++;
    }
```

#### Responses

원본:

```csharp
public string Responses(long id) => publicResponses.TryGetValue(id, out var rows) && rows.Count > 0 ? string.Join("\n\n", rows) : "이 기사에 직접 연결된 공개 발언은 아직 없습니다.";
```

수정본:

```csharp
public string Responses(long id)
    {
        if (!publicResponses.TryGetValue(id,out var rows) || rows.Count == 0) { return GameLocale.Text("이 기사에 직접 연결된 공개 발언은 아직 없습니다."); }
        var result = new System.Text.StringBuilder();
        foreach (var row in rows)
        {
            if (result.Length > 0) { result.Append("\n\n"); }
            result.Append(row.name).Append("  ").Append(LocalizedChat.Text(row.message,row.message.text));
        }
        return result.ToString();
    }
```

### Assets/Scripts/News/NewsDeskUI.cs

#### RefreshList

원본:

```csharp
private void RefreshList()
    {
        var news = NewsManager.Instance;
        if (news == null || navigation == null) { return; }
        float scroll = listScroll.verticalNormalizedPosition;
        version = news.ArticleVersion; alertVersion = terminal.records.Version; visible.Clear();
        var stock = StockManager.Instance?.CurrentStock;
        if (mode == "alerts") { RefreshAlerts(); }
        else { foreach (var article in news.Articles)
        {
            var relatedStock = FindStock(article.symbol);
            bool held = relatedStock != null && navigation.account.GetOwnedVolume(relatedStock) > 0;
            if (NewsWriting.Matches(article,query,category,mode == "current" ? stock?.symbol : null,mode == "owned",held,mode == "rumors"))
            { visible.Add(article); }
        } }
        if (selected == null || !visible.Contains(selected)) { selected = visible.Count > 0 ? visible[0] : null; articleScroll.verticalNormalizedPosition = 1; }
        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i < visible.Count; rows[i].gameObject.SetActive(active); if (!active) { continue; }
            var rect = (RectTransform)rows[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchorMax = new Vector2(1,1); rect.anchoredPosition = new Vector2(0,-i * 157); rect.sizeDelta = new Vector2(0,156);
            var article = visible[i]; Set(titles[i],article.title);
            Set(meta[i],(string.IsNullOrEmpty(article.company) ? article.source : article.company) + "  ·  " + article.time +
                (article.credibility < .5 ? " · 미확인" : article.correction ? " · 후속" : ""));
            rows[i].image.color = article == selected ? Selected : Color.clear;
        }
        listContent.sizeDelta = new Vector2(0,visible.Count * 157); listScroll.verticalNormalizedPosition = scroll;
        empty.gameObject.SetActive(visible.Count == 0); Set(countText, (mode == "alerts" ? "시장 알림 " : "최근 기사 ") + visible.Count);
        TabletControlStyle.Emphasize(all,mode == "all"); TabletControlStyle.Emphasize(owned,mode == "owned");
        TabletControlStyle.Emphasize(current,mode == "current"); TabletControlStyle.Emphasize(rumors,mode == "rumors");
        Text(categoryButton,"분야 · " + (categoryIndex < 0 ? "전체" : news.Writing.narratives[categoryIndex].label) + " ▼");
        RenderArticle();
    }
```

수정본:

```csharp
private void RefreshList()
    {
        var news = NewsManager.Instance;
        if (news == null || navigation == null) { return; }
        float scroll = listScroll.verticalNormalizedPosition;
        version = news.ArticleVersion; alertVersion = terminal.records.Version; visible.Clear();
        var stock = StockManager.Instance?.CurrentStock;
        if (mode == "alerts") { RefreshAlerts(); }
        else { foreach (var article in news.Articles)
        {
            var relatedStock = FindStock(article.symbol);
            bool held = relatedStock != null && navigation.account.GetOwnedVolume(relatedStock) > 0;
            if (NewsWriting.Matches(article,"",category,mode == "current" ? stock?.symbol : null,mode == "owned",held,mode == "rumors") && MatchesQuery(article))
            { visible.Add(article); }
        } }
        bool changedSelection = selected == null || !visible.Contains(selected);
        if (changedSelection) { selected = visible.Count > 0 ? visible[0] : null; }
        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i < visible.Count; rows[i].gameObject.SetActive(active); if (!active) { continue; }
            var rect = (RectTransform)rows[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
            rect.anchorMax = new Vector2(1,1); rect.anchoredPosition = new Vector2(0,-i * 157); rect.sizeDelta = new Vector2(0,156);
            var article = visible[i]; Set(titles[i],article.title);
            Set(meta[i],(string.IsNullOrEmpty(article.company) ? article.source : article.company) + "  ·  " + article.time +
                (article.credibility < .5 ? " · 미확인" : article.correction ? " · 후속" : ""));
            rows[i].image.color = article == selected ? Selected : Color.clear;
        }
        listContent.sizeDelta = new Vector2(0,visible.Count * 157); listScroll.verticalNormalizedPosition = scroll;
        empty.gameObject.SetActive(visible.Count == 0); Set(countText, (mode == "alerts" ? "시장 알림 " : "최근 기사 ") + visible.Count);
        TabletControlStyle.Emphasize(all,mode == "all"); TabletControlStyle.Emphasize(owned,mode == "owned");
        TabletControlStyle.Emphasize(current,mode == "current"); TabletControlStyle.Emphasize(rumors,mode == "rumors");
        Text(categoryButton,"분야 · " + (categoryIndex < 0 ? "전체" : news.Writing.narratives[categoryIndex].label) + " ▼");
        RenderArticle();
        if (changedSelection) { articleScroll.verticalNormalizedPosition = 1; }
    }
```

#### RefreshAlerts

원본:

```csharp
private void RefreshAlerts()
    {
        var removed = new List<DeviceMarketJournal.Entry>();
        foreach (var entry in alerts.Keys) { if (!terminal.records.Events.Contains(entry)) { removed.Add(entry); } }
        foreach (var entry in removed) { alerts.Remove(entry); }
        foreach (var entry in terminal.records.Events)
        {
            if (entry.Article != null) { continue; }
            if (!alerts.TryGetValue(entry, out var article))
            {
                article = new NewsArticle { symbol = entry.Stock?.symbol, company = entry.Stock?.stockName, title = entry.Title,
                    category = "notice", categoryLabel = "시장 알림", source = entry.Source, verification = entry.Certainty,
                    time = entry.Time, credibility = 1, summary = entry.Certainty,
                    body = entry.Stock == null ? "게임 내 시장과 진행 상태에 관한 알림입니다." :
                        "발생 직전 60초 · 실제 체결 " + entry.Volume.ToString("N0") + "주\n매수 주도 비중 " +
                        (entry.Volume > 0 ? (entry.BuyVolume / entry.Volume * 100).ToString("0.0") + "%" : "집계 없음") +
                        "\n매도 호가를 소진한 체결량 / 전체 체결량입니다." };
                alerts.Add(entry, article);
            }
            if (NewsWriting.Matches(article,query,"all",null,false,false,false)) { visible.Add(article); }
        }
    }
```

수정본:

```csharp
private void RefreshAlerts()
    {
        var removed = new List<DeviceMarketJournal.Entry>();
        foreach (var entry in alerts.Keys) { if (!terminal.records.Events.Contains(entry)) { removed.Add(entry); } }
        foreach (var entry in removed) { alerts.Remove(entry); }
        foreach (var entry in terminal.records.Events)
        {
            if (entry.Article != null) { continue; }
            if (!alerts.TryGetValue(entry, out var article))
            {
                article = new NewsArticle { symbol = entry.Stock?.symbol, company = entry.Stock?.stockName, title = entry.Title,
                    category = "notice", categoryLabel = "시장 알림", source = entry.Source, verification = entry.Certainty,
                    time = entry.Time, credibility = 1, summary = entry.Certainty,
                    body = entry.Stock == null ? "게임 내 시장과 진행 상태에 관한 알림입니다." :
                        "발생 직전 60초 · 실제 체결 " + entry.Volume.ToString("N0") + "주\n매수 주도 비중 " +
                        (entry.Volume > 0 ? (entry.BuyVolume / entry.Volume * 100).ToString("0.0") + "%" : "집계 없음") +
                        "\n매도 호가를 소진한 체결량 / 전체 체결량입니다." };
                alerts.Add(entry, article);
            }
            if (MatchesQuery(article)) { visible.Add(article); }
        }
    }
```

#### MatchesQuery

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
private bool MatchesQuery(NewsArticle article)
    {
        if (NewsWriting.Matches(article,query,"all",null,false,false,false)) { return true; }
        string needle = query.Trim();
        return (GameLocale.Text(article.title) + " " + GameLocale.Text(article.company)).IndexOf(needle,StringComparison.OrdinalIgnoreCase) >= 0;
    }
```

#### SelectRow

원본:

```csharp
private void SelectRow(int row)
    {
        if (row < 0 || row >= visible.Count) { return; }
        selected = visible[row]; articleScroll.verticalNormalizedPosition = 1;
        foreach (var button in rows) { button.image.color = Color.clear; }
        rows[row].image.color = Selected; RenderArticle();
    }
```

수정본:

```csharp
private void SelectRow(int row)
    {
        if (row < 0 || row >= visible.Count) { return; }
        selected = visible[row];
        foreach (var button in rows) { button.image.color = Color.clear; }
        rows[row].image.color = Selected; RenderArticle();
        articleScroll.verticalNormalizedPosition = 1;
    }
```

#### SetArticle

원본:

```csharp
private void SetArticle(string text)
    {
        float previous = articleScroll.verticalNormalizedPosition; Set(articleText,text);
        float width = Math.Max(100,articleScroll.viewport.rect.width - 16);
        float height = articleText.GetPreferredValues(text,width,0).y + 40;
        articleContent.sizeDelta = new Vector2(0,height);
        articleText.rectTransform.anchorMin = new Vector2(0,1); articleText.rectTransform.anchorMax = Vector2.one;
        articleText.rectTransform.pivot = new Vector2(0,1); articleText.rectTransform.anchoredPosition = Vector2.zero;
        articleText.rectTransform.sizeDelta = new Vector2(-16,height);
        articleScroll.verticalNormalizedPosition = previous;
    }
```

수정본:

```csharp
private void SetArticle(string text)
    {
        float previous = articleScroll.verticalNormalizedPosition; Set(articleText,text);
        float width = Math.Max(100,articleScroll.viewport.rect.width - 16);
        float height = articleText.GetPreferredValues(GameLocale.Text(text),width,0).y + 40;
        articleContent.sizeDelta = new Vector2(0,height);
        articleText.rectTransform.anchorMin = new Vector2(0,1); articleText.rectTransform.anchorMax = Vector2.one;
        articleText.rectTransform.pivot = new Vector2(0,1); articleText.rectTransform.anchoredPosition = Vector2.zero;
        articleText.rectTransform.sizeDelta = new Vector2(-16,height);
        articleScroll.verticalNormalizedPosition = previous;
    }
```

#### RefreshDraft

원본:

```csharp
private void RefreshDraft()
    {
        previewStock = StockManager.Instance?.CurrentStock;
        var publisher = FakeNewsManager.Instance; var news = NewsManager.Instance;
        if (publisher == null || news == null || previewStock == null) { return; }
        var theme = news.Writing.narratives[draftCategoryIndex];
        draft = publisher.PreviewPlayer(previewStock,theme.key,optimistic,variant);
        Text(draftStock,"대상 · " + previewStock.stockName + " ▼"); Text(draftCategory,"분야 · " + theme.label + " ▼");
        TabletControlStyle.Emphasize(positive,optimistic); TabletControlStyle.Emphasize(negative,!optimistic);
        string text = "<b>" + Safe(draft.title) + "</b>\n\n" + Safe(draft.summary) + "\n\n" + Safe(draft.body);
        Set(draftText,text); var content = (RectTransform)draftText.transform.parent;
        float width = Math.Max(100,((RectTransform)content.parent).rect.width - 16);
        float height = draftText.GetPreferredValues(text,width,0).y + 30; content.sizeDelta = new Vector2(0,height);
        Rect(draftText.rectTransform,0,0,1,1); draftText.rectTransform.offsetMax = new Vector2(-16,0);
        if (Time.unscaledTime >= resultUntil) { Set(resultText,"공개되면 투자자마다 다르게 받아들입니다.\n후속 확인 보도가 나올 수 있습니다."); }
    }
```

수정본:

```csharp
private void RefreshDraft()
    {
        previewStock = StockManager.Instance?.CurrentStock;
        var publisher = FakeNewsManager.Instance; var news = NewsManager.Instance;
        if (publisher == null || news == null || previewStock == null) { return; }
        var theme = news.Writing.narratives[draftCategoryIndex];
        draft = publisher.PreviewPlayer(previewStock,theme.key,optimistic,variant);
        Text(draftStock,"대상 · " + previewStock.stockName + " ▼"); Text(draftCategory,"분야 · " + theme.label + " ▼");
        TabletControlStyle.Emphasize(positive,optimistic); TabletControlStyle.Emphasize(negative,!optimistic);
        string text = "<b>" + Safe(draft.title) + "</b>\n\n" + Safe(draft.summary) + "\n\n" + Safe(draft.body);
        Set(draftText,text); var content = (RectTransform)draftText.transform.parent;
        float width = Math.Max(100,((RectTransform)content.parent).rect.width - 16);
        float height = draftText.GetPreferredValues(GameLocale.Text(text),width,0).y + 30; content.sizeDelta = new Vector2(0,height);
        Rect(draftText.rectTransform,0,0,1,1); draftText.rectTransform.offsetMax = new Vector2(-16,0);
        if (Time.unscaledTime >= resultUntil) { Set(resultText,"공개되면 투자자마다 다르게 받아들입니다.\n후속 확인 보도가 나올 수 있습니다."); }
    }
```

#### Update

원본:

```csharp
void Update()
    {
        if (navigation == null || navigation.Locked || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .3f;
        var news = NewsManager.Instance; if (news == null) { return; }
        var stock = StockManager.Instance?.CurrentStock;
        if (version != news.ArticleVersion || stock != lastStock && mode == "current" || mode == "alerts" && alertVersion != terminal.records.Version) { RefreshList(); }
        lastStock = stock;
        int responses = CompanyNewsAudience.Instance == null ? 0 : CompanyNewsAudience.Instance.Version;
        if (responses != responseVersion) { responseVersion = responses; RenderArticle(); }
        if (!studio.activeSelf) { return; }
        if (stock != previewStock) { RefreshDraft(); }
        var publisher = FakeNewsManager.Instance;
        double remaining = publisher == null ? 0 : publisher.PlayerCooldown;
        Set(cooldownText,remaining > 0 ? "다음 발행까지 " + FakeNewsManager.FormatCooldown(remaining) + " · 현실시간" : "발행 가능 · 발행 후 현실시간 10분 대기");
        publish.interactable = publisher != null && publisher.PlayerUnlocked && remaining <= 0 && MarketDayController.CanTrade && previewStock?.AcceptsOrders == true;
        Text(publish,remaining > 0 ? "대기 " + FakeNewsManager.FormatCooldown(remaining) : "이 초안 발행");
    }
```

수정본:

```csharp
void Update()
    {
        if (navigation == null || navigation.Locked || Time.unscaledTime < next) { return; }
        next = Time.unscaledTime + .3f;
        var news = NewsManager.Instance; if (news == null) { return; }
        if (localeRevision != GameLocale.Revision)
        {
            localeRevision = GameLocale.Revision; RefreshList();
            if (studio.activeSelf) { RefreshDraft(); }
        }
        var stock = StockManager.Instance?.CurrentStock;
        if (version != news.ArticleVersion || stock != lastStock && mode == "current" || mode == "alerts" && alertVersion != terminal.records.Version) { RefreshList(); }
        lastStock = stock;
        int responses = CompanyNewsAudience.Instance == null ? 0 : CompanyNewsAudience.Instance.Version;
        if (responses != responseVersion) { responseVersion = responses; RenderArticle(); }
        if (!studio.activeSelf) { return; }
        if (stock != previewStock) { RefreshDraft(); }
        var publisher = FakeNewsManager.Instance;
        double remaining = publisher == null ? 0 : publisher.PlayerCooldown;
        Set(cooldownText,remaining > 0 ? "다음 발행까지 " + FakeNewsManager.FormatCooldown(remaining) + " · 현실시간" : "발행 가능 · 발행 후 현실시간 10분 대기");
        publish.interactable = publisher != null && publisher.PlayerUnlocked && remaining <= 0 && MarketDayController.CanTrade && previewStock?.AcceptsOrders == true;
        Text(publish,remaining > 0 ? "대기 " + FakeNewsManager.FormatCooldown(remaining) : "이 초안 발행");
    }
```

#### Label

원본:

```csharp
private TextMeshProUGUI Label(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color,bool bold = false)
    {
        var rect = Area(parent,name,x,y,w,h); rect.name = "Text :: " + name;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); Style(label,size,color,bold); label.text = text; return label;
    }
```

수정본:

```csharp
private TextMeshProUGUI Label(Transform parent,string name,string text,float x,float y,float w,float h,float size,Color color,bool bold = false)
    {
        var rect = Area(parent,name,x,y,w,h); rect.name = "Text :: " + name;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); Style(label,size,color,bold); GameLocalizedText.Set(label,text); return label;
    }
```

### Assets/Scripts/News/NewsListItem.cs

#### Set

원본:

```csharp
public void Set(string symbol, float criticalValue, string title)
    {
        if (textSymbol != null) textSymbol.text = symbol ?? "";
        if (textTitle != null) textTitle.text = title ?? "";

        float pct = (criticalValue - 1f) * 100f;

        if (imageGoodBad != null)
            imageGoodBad.color = criticalValue >= 1f ? goodColor : badColor;
    }
```

수정본:

```csharp
public void Set(string symbol, float criticalValue, string title)
    {
        if (textSymbol != null) GameLocalizedText.Set(textSymbol,symbol ?? "");
        if (textTitle != null) GameLocalizedText.Set(textTitle,title ?? "");

        float pct = (criticalValue - 1f) * 100f;

        if (imageGoodBad != null)
            imageGoodBad.color = criticalValue >= 1f ? goodColor : badColor;
    }
```

### Assets/Scripts/NightShift/ChatSocialData.cs

#### Next

원본:

```csharp
public static long Next() => ++last;
```

수정본:

```csharp
public double Next()
    { uint x = State; x ^= x << 13; x ^= x >> 17; x ^= x << 5; State = x; return x / 4294967296d; }
```

SocialMessage에 선택된 문장·대상·접미사·슬롯 스냅샷 필드를 추가했습니다. SocialSpeechValue는 원래 표시값과 원시 수치를 함께 저장합니다.

### Assets/Scripts/NightShift/ChatSocialMentions.cs

#### Format

원본:

```csharp
public string Format(string text)
    {
        if (string.IsNullOrEmpty(text))
        { return text ?? ""; }
        var result = new StringBuilder(text.Length + 64);
        int index = 0;
        while (index < text.Length)
        {
            int start = text[index] == '@' ? index + 1 : index;
            string symbol = null;
            int end = index == 0 || !Word(text[index - 1]) ? MatchEnd(text, start, text[index] == '@', out symbol) : -1;
            if (end > start)
            {
                if (symbol != null) { result.Append("<link=\"stock:").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(symbol))).Append("\">").Append(StockMarkOpen); }
                else { result.Append(MarkOpen); }
                AppendLiteral(result, text, index, end);
                result.Append(symbol == null ? "</mark>" : "</color></mark></link>"); index = end;
            }
            else { AppendLiteral(result, text, index, index + 1); index++; }
        }
        return result.ToString();
    }
```

수정본:

```csharp
public string Format(string text)
    {
        if (string.IsNullOrEmpty(text))
        { return text ?? ""; }
        var result = new StringBuilder(text.Length + 64);
        int index = 0;
        while (index < text.Length)
        {
            int start = text[index] == '@' ? index + 1 : index;
            string symbol = null;
            int end = index == 0 || !Word(text[index - 1]) || Japanese(text[index - 1]) ? MatchEnd(text, start, text[index] == '@', out symbol) : -1;
            if (end > start)
            {
                if (symbol != null) { result.Append("<link=\"stock:").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(symbol))).Append("\">").Append(StockMarkOpen); }
                else { result.Append(MarkOpen); }
                AppendLiteral(result, text, index, end);
                result.Append(symbol == null ? "</mark>" : "</color></mark></link>"); index = end;
            }
            else { AppendLiteral(result, text, index, index + 1); index++; }
        }
        return result.ToString();
    }
```

#### Japanese

원본:

```csharp
// 신규 함수
```

수정본:

```csharp
private static bool Japanese(char letter) => letter >= '\u3040' && letter <= '\u30ff' || letter >= '\u3400' && letter <= '\u9fff';
```

#### BoundaryAfter

원본:

```csharp
private static bool BoundaryAfter(string text, int index)
    {
        if (index >= text.Length || !Word(text[index]))
        { return true; }
        foreach (string particle in Particles)
        {
            if (index + particle.Length > text.Length || string.CompareOrdinal(text, index, particle, 0, particle.Length) != 0)
            { continue; }
            int end = index + particle.Length;
            if (end == text.Length || !Word(text[end])) { return true; }
            if (particle == "님")
            {
                foreach (string suffix in Particles)
                {
                    int tail = end + suffix.Length;
                    if (tail <= text.Length && string.CompareOrdinal(text, end, suffix, 0, suffix.Length) == 0 && (tail == text.Length || !Word(text[tail]))) { return true; }
                }
            }
        }
        return false;
    }
```

수정본:

```csharp
private static bool BoundaryAfter(string text, int index)
    {
        if (index >= text.Length || !Word(text[index]))
        { return true; }
        foreach (string particle in Particles)
        {
            if (index + particle.Length > text.Length || string.CompareOrdinal(text, index, particle, 0, particle.Length) != 0)
            { continue; }
            int end = index + particle.Length;
            // Japanese has no space between a particle and the following clause.
            if (Japanese(particle[0])) { return true; }
            if (end == text.Length || !Word(text[end])) { return true; }
            if (particle == "님")
            {
                foreach (string suffix in Particles)
                {
                    int tail = end + suffix.Length;
                    if (tail <= text.Length && string.CompareOrdinal(text, end, suffix, 0, suffix.Length) == 0 && (tail == text.Length || !Word(text[tail]))) { return true; }
                }
            }
        }
        return false;
    }
```

### Assets/Scripts/NightShift/ChatSocialMenuUnity.cs

#### Label

원본:

```csharp
private TextMeshProUGUI Label(RectTransform parent, string text, TMP_FontAsset font, float y, float height)
    {
        var go = new GameObject("Text :: " + text, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.offsetMin = new Vector2(14 * chat.layoutScale, -(y + height) * chat.layoutScale);
        rect.offsetMax = new Vector2(-14 * chat.layoutScale, -y * chat.layoutScale);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; label.text = text;
        label.fontSize = 25 * chat.layoutScale; label.enableAutoSizing = false; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = new Color(.90f, .92f, .94f); label.raycastTarget = false;
        return label;
    }
```

수정본:

```csharp
private TextMeshProUGUI Label(RectTransform parent, string text, TMP_FontAsset font, float y, float height)
    {
        var go = new GameObject("Text :: " + text, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.offsetMin = new Vector2(14 * chat.layoutScale, -(y + height) * chat.layoutScale);
        rect.offsetMax = new Vector2(-14 * chat.layoutScale, -y * chat.layoutScale);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; GameLocalizedText.Set(label,text);
        label.fontSize = 25 * chat.layoutScale; label.enableAutoSizing = false; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = new Color(.90f, .92f, .94f); label.raycastTarget = false;
        return label;
    }
```

#### Open

원본:

```csharp
public void Open(int id, RectTransform nickname)
    {
        var society = InvestorSociety.Instance;
        roomId = chat.SelectedRoom; botId = id;
        if (society == null || roomId < 1 || roomId > InvestorSociety.PlayerRoomCount || !society.IsRoomOpen(roomId) ||
            !society.SocialBots.TryGetValue(id, out var bot) || bot.Social.Psychology.Room != roomId)
        { Close(); return; }
        var parent = (RectTransform)panel.parent;
        Vector3 local = parent.InverseTransformPoint(nickname.position);
        panel.anchoredPosition = new Vector2(Mathf.Clamp(local.x, parent.rect.xMin, parent.rect.xMax - panel.rect.width),
            Mathf.Clamp(local.y, parent.rect.yMin + panel.rect.height, parent.rect.yMax));
        title.text = bot.Social.Nickname;
        panel.SetAsLastSibling(); panel.gameObject.SetActive(true);
    }
```

수정본:

```csharp
public void Open(int id, RectTransform nickname)
    {
        var society = InvestorSociety.Instance;
        roomId = chat.SelectedRoom; botId = id;
        if (society == null || roomId < 1 || roomId > InvestorSociety.PlayerRoomCount || !society.IsRoomOpen(roomId) ||
            !society.SocialBots.TryGetValue(id, out var bot) || bot.Social.Psychology.Room != roomId)
        { Close(); return; }
        var parent = (RectTransform)panel.parent;
        Vector3 local = parent.InverseTransformPoint(nickname.position);
        panel.anchoredPosition = new Vector2(Mathf.Clamp(local.x, parent.rect.xMin, parent.rect.xMax - panel.rect.width),
            Mathf.Clamp(local.y, parent.rect.yMin + panel.rect.height, parent.rect.yMax));
        GameLocalizedText.Set(title,bot.Social.Nickname,true);
        panel.SetAsLastSibling(); panel.gameObject.SetActive(true);
    }
```

### Assets/Scripts/NightShift/ChatSocialSpeech.cs

#### RenderSocialText

원본:

```csharp
public static string RenderSocialText(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, string target, IEnumerable<SocialMessage> recent = null)
    {
        message.debug ??= new SocialMessageDebug();
        message.debug.speechDisposition = mind.Traits.SpeechLabel; message.debug.speechKey = null; message.debug.speechVoice = VoiceLabel(mind);
        var random = CosmeticRandom(mind, message, 0x58c43af1u);
        double communityChance = ChatSocialConfig.Current.Get(mind.Traits.Serious ? "communitySeriousChance" : "communityPlayfulChance");
        string phrase = ContextualSocialText(template, message, parent);
        bool community = false;
        if (random.Next() < communityChance)
        {
            var group = CommunityGroup(template, message, parent);
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
        phrase = VoiceStockParticles(phrase, message.facts.stockName ?? message.facts.symbol);
        // Trading instructions retain exact limit prices, sizes and fill accounting. Only conversation P/L slots are abbreviated.
        bool colloquial = !message.facts.signal && (community || random.Next() < ChatSocialConfig.Current.Get(mind.Traits.Serious ? "seriousColloquialNumberChance" : "colloquialNumberChance"));
        int dialect = random.Range(0, 3);
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
        return community || voiced ? text : DecorateSocialText(mind, message, text);
    }
```

수정본:

```csharp
public static string RenderSocialText(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, string target, IEnumerable<SocialMessage> recent = null)
    {
        message.debug ??= new SocialMessageDebug();
        message.debug.speechDisposition = mind.Traits.SpeechLabel; message.debug.speechKey = null; message.debug.speechVoice = VoiceLabel(mind);
        var random = CosmeticRandom(mind, message, 0x58c43af1u);
        double communityChance = ChatSocialConfig.Current.Get(mind.Traits.Serious ? "communitySeriousChance" : "communityPlayfulChance");
        string phrase = ContextualSocialText(template, message, parent);
        bool community = false;
        if (random.Next() < communityChance)
        {
            var group = CommunityGroup(template, message, parent);
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
```

### Assets/Scripts/NightShift/CompactGrowthUI.cs

#### Configure

원본:

```csharp
public void Configure(Button action,Button close)
    {
        if (configured)
        { return; }
        configured = true; primary = action; actionLabel = action.GetComponentInChildren<TextMeshProUGUI>();
        Layout((RectTransform)transform,.015f,.012f,.97f,.976f);
        Layout(title.rectTransform,.045f,.04f,.74f,.075f); title.fontSize = 52;
        Layout((RectTransform)details.transform,.83f,.045f,.125f,.06f);
        var detailsLabel = details.GetComponentInChildren<TextMeshProUGUI>(); detailsLabel.text = "조건 보기"; detailsLabel.fontSize = 30;
        nextStep = Caption("Next Step",.045f,.15f,.91f,.085f,49);
        hint = Caption("Action Hint",.045f,.25f,.91f,.055f,31); hint.color = new Color32(166,177,186,255);
        Layout((RectTransform)overview.transform,.045f,.35f,.91f,.36f);
        Layout(turnover.rectTransform,.10f,0,.26f,.42f); Layout(profit.rectTransform,.64f,0,.26f,.42f);
        turnoverCaption = overview.transform.Find("Text :: Turnover Caption").GetComponent<TextMeshProUGUI>();
        profitCaption = overview.transform.Find("Text :: Profit Caption").GetComponent<TextMeshProUGUI>();
        turnoverCaption.fontSize = profitCaption.fontSize = 32;
        turnoverAmount.fontSize = profitAmount.fontSize = 38;
        Layout(detailText.rectTransform,.045f,.35f,.91f,.38f); detailText.fontSize = 32;
        Layout((RectTransform)action.transform,.045f,.80f,.66f,.115f); actionLabel.fontSize = 43;
        Layout((RectTransform)close.transform,.73f,.82f,.225f,.085f); close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;
        TabletControlStyle.Button(primary,true);
        primary.transition = Selectable.Transition.ColorTint;
    }
```

수정본:

```csharp
public void Configure(Button action,Button close)
    {
        if (configured)
        { return; }
        configured = true; primary = action; actionLabel = action.GetComponentInChildren<TextMeshProUGUI>();
        Layout((RectTransform)transform,.015f,.012f,.97f,.976f);
        Layout(title.rectTransform,.045f,.04f,.74f,.075f); title.fontSize = 52;
        Layout((RectTransform)details.transform,.83f,.045f,.125f,.06f);
        var detailsLabel = details.GetComponentInChildren<TextMeshProUGUI>(); GameLocalizedText.Set(detailsLabel,"조건 보기"); detailsLabel.fontSize = 30;
        nextStep = Caption("Next Step",.045f,.15f,.91f,.085f,49);
        hint = Caption("Action Hint",.045f,.25f,.91f,.055f,31); hint.color = new Color32(166,177,186,255);
        Layout((RectTransform)overview.transform,.045f,.35f,.91f,.36f);
        Layout(turnover.rectTransform,.10f,0,.26f,.42f); Layout(profit.rectTransform,.64f,0,.26f,.42f);
        turnoverCaption = overview.transform.Find("Text :: Turnover Caption").GetComponent<TextMeshProUGUI>();
        profitCaption = overview.transform.Find("Text :: Profit Caption").GetComponent<TextMeshProUGUI>();
        turnoverCaption.fontSize = profitCaption.fontSize = 32;
        turnoverAmount.fontSize = profitAmount.fontSize = 38;
        Layout(detailText.rectTransform,.045f,.35f,.91f,.38f); detailText.fontSize = 32;
        Layout((RectTransform)action.transform,.045f,.80f,.66f,.115f); actionLabel.fontSize = 43;
        Layout((RectTransform)close.transform,.73f,.82f,.225f,.085f); close.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;
        TabletControlStyle.Button(primary,true);
        primary.transition = Selectable.Transition.ColorTint;
    }
```

#### Caption

원본:

```csharp
private TextMeshProUGUI Caption(string name,float x,float y,float w,float h,float size)
    {
        var label = Instantiate(title,transform); label.name = "Text :: Growth " + name; label.text = ""; label.fontSize = size;
        label.raycastTarget = false; label.enableAutoSizing = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis; Layout(label.rectTransform,x,y,w,h); return label;
    }
```

수정본:

```csharp
private TextMeshProUGUI Caption(string name,float x,float y,float w,float h,float size)
    {
        var label = Instantiate(title,transform); label.name = "Text :: Growth " + name; GameLocalizedText.Set(label,""); label.fontSize = size;
        label.raycastTarget = false; label.enableAutoSizing = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis; Layout(label.rectTransform,x,y,w,h); return label;
    }
```

### Assets/Scripts/NightShift/CorporateControlUI.cs

#### Update

원본:

```csharp
void Update()
    {
        if (Time.unscaledTime < next || WorldDramaDirector.Instance == null || StockManager.Instance == null)
        { return; }
        next = Time.unscaledTime + .5f;
        var current = StockManager.Instance.CurrentStock;
        if (WorldDramaDirector.Instance.Controls(current)) { controlled = current; }
        bool can = WorldDramaDirector.Instance.Controls(controlled);
        management.interactable = investment.interactable = dividend.interactable = can && MarketDayController.CanTrade;
        acquisition.interactable = can && current != controlled && MarketDayController.CanTrade;
        string text = can ? controlled.stockName + " 경영권 확보 · 매입 대상 " + current.stockName : "경영권: 실제 지분 50% 초과 필요";
        if (status.text != text) { status.text = text; }
    }
```

수정본:

```csharp
void Update()
    {
        if (Time.unscaledTime < next || WorldDramaDirector.Instance == null || StockManager.Instance == null)
        { return; }
        next = Time.unscaledTime + .5f;
        var current = StockManager.Instance.CurrentStock;
        if (WorldDramaDirector.Instance.Controls(current)) { controlled = current; }
        bool can = WorldDramaDirector.Instance.Controls(controlled);
        management.interactable = investment.interactable = dividend.interactable = can && MarketDayController.CanTrade;
        acquisition.interactable = can && current != controlled && MarketDayController.CanTrade;
        string text = can ? controlled.stockName + " 경영권 확보 · 매입 대상 " + current.stockName : "경영권: 실제 지분 50% 초과 필요";
        if (!GameLocalizedText.Matches(status,text)) { GameLocalizedText.Set(status,text); }
    }
```

### Assets/Scripts/NightShift/DailyIncomeController.cs

#### NameCompany

원본:

```csharp
public bool NameCompany(string name)
    {
        if (!ValidName(name) || StockManager.Instance != null && StockManager.Instance.SetupConfirmed)
        { return false; }
        CompanyName = name; return true;
    }
```

수정본:

```csharp
public bool NameCompany(string name)
    {
        if (!ValidName(name) || StockManager.Instance != null && StockManager.Instance.SetupConfirmed)
        { return false; }
        CompanyName = name; GameLocale.ProtectLiteral(name); return true;
    }
```

#### Restore

원본:

```csharp
public bool Restore(SaveData saved,string worldId)
    {
        if (!CanRestore(saved,worldId) || !Ledger.Restore(saved.ledger,MarketDayController.Instance == null ? 1 : MarketDayController.Instance.Day))
        { return false; }
        CompanyName = saved.company; realizedAtOpen = decimal.Parse(saved.realizedAtOpen,CultureInfo.InvariantCulture); return true;
    }
```

수정본:

```csharp
public bool Restore(SaveData saved,string worldId)
    {
        if (!CanRestore(saved,worldId) || !Ledger.Restore(saved.ledger,MarketDayController.Instance == null ? 1 : MarketDayController.Instance.Day))
        { return false; }
        CompanyName = saved.company; GameLocale.ProtectLiteral(saved.company);
        realizedAtOpen = decimal.Parse(saved.realizedAtOpen,CultureInfo.InvariantCulture); return true;
    }
```

### Assets/Scripts/NightShift/DailyIncomeUI.cs

#### CompanyField

원본:

```csharp
public static TMP_InputField CompanyField(PlayerCompanySetupUI setup)
    {
        if (setup.retailIntroduction == null)
        { return null; }
        var root = setup.retailIntroduction.transform;
        var caption = Instantiate(setup.retailPreview,root); caption.name = "Text :: Private Company Name"; caption.text = "회사 이름";
        Place(caption.rectTransform,root,0,.62f,.51f,.045f); caption.raycastTarget = false;
        var field = Instantiate(setup.companyName,root); field.name = "Input :: Private Company Name";
        Place((RectTransform)field.transform,root,0,.675f,.51f,.075f); field.gameObject.SetActive(true); field.interactable = true;
        field.onValueChanged.RemoveAllListeners(); field.characterLimit = 20; field.contentType = TMP_InputField.ContentType.Standard; field.richText = false;
        field.SetTextWithoutNotify(DailyIncomeController.Instance?.CompanyName ?? "내 작은 회사");
        TabletControlStyle.Input(field); field.textComponent.alignment = TextAlignmentOptions.MidlineLeft; field.textComponent.fontSize = 30f;
        field.onValueChanged.AddListener(_ => setup.Refresh());
        return field;
    }
```

수정본:

```csharp
public static TMP_InputField CompanyField(PlayerCompanySetupUI setup)
    {
        if (setup.retailIntroduction == null)
        { return null; }
        var root = setup.retailIntroduction.transform;
        var caption = Instantiate(setup.retailPreview,root); caption.name = "Text :: Private Company Name"; GameLocalizedText.Set(caption,"회사 이름");
        Place(caption.rectTransform,root,0,.62f,.51f,.045f); caption.raycastTarget = false;
        var field = Instantiate(setup.companyName,root); field.name = "Input :: Private Company Name";
        Place((RectTransform)field.transform,root,0,.675f,.51f,.075f); field.gameObject.SetActive(true); field.interactable = true;
        field.onValueChanged.RemoveAllListeners(); field.characterLimit = 20; field.contentType = TMP_InputField.ContentType.Standard; field.richText = false;
        field.SetTextWithoutNotify(DailyIncomeController.Instance?.CompanyName ?? "내 작은 회사");
        TabletControlStyle.Input(field); field.textComponent.alignment = TextAlignmentOptions.MidlineLeft; field.textComponent.fontSize = 30f;
        field.onValueChanged.AddListener(_ => setup.Refresh());
        return field;
    }
```

#### Open

원본:

```csharp
private void Open()
    {
        if (roomMode && DailyIncomeController.Instance != null)
        { feeInput.SetTextWithoutNotify(DailyIncomeController.Instance.Ledger.NextFee.ToString()); result.text = ""; }
        if (roomMode && !panel.activeSelf) { originalSibling = transform.GetSiblingIndex(); transform.SetAsLastSibling(); }
        panel.SetActive(true); panel.transform.SetAsLastSibling(); next = 0f; Refresh();
    }
```

수정본:

```csharp
private void Open()
    {
        if (roomMode && DailyIncomeController.Instance != null)
        { feeInput.SetTextWithoutNotify(DailyIncomeController.Instance.Ledger.NextFee.ToString()); GameLocalizedText.Set(result,""); }
        if (roomMode && !panel.activeSelf) { originalSibling = transform.GetSiblingIndex(); transform.SetAsLastSibling(); }
        panel.SetActive(true); panel.transform.SetAsLastSibling(); next = 0f; Refresh();
    }
```

#### Apply

원본:

```csharp
private void Apply()
    {
        var income = DailyIncomeController.Instance;
        if (income == null)
        { return; }
        income.SetNextFee(feeInput.text); result.text = income.Feedback; next = 0f;
    }
```

수정본:

```csharp
private void Apply()
    {
        var income = DailyIncomeController.Instance;
        if (income == null)
        { return; }
        income.SetNextFee(feeInput.text); GameLocalizedText.Set(result,income.Feedback); next = 0f;
    }
```

#### Label

원본:

```csharp
private TextMeshProUGUI Label(Transform parent,string name,string text,float x,float y,float w,float h,float size)
    {
        var rect = Box(name,parent,x,y,w,h,false); rect.name = "Text :: " + name;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size*scale; label.fontStyle = FontStyles.Normal;
        label.raycastTarget = false; label.enableAutoSizing = false; label.color = Ink; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.text = text; return label;
    }
```

수정본:

```csharp
private TextMeshProUGUI Label(Transform parent,string name,string text,float x,float y,float w,float h,float size)
    {
        var rect = Box(name,parent,x,y,w,h,false); rect.name = "Text :: " + name;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = size*scale; label.fontStyle = FontStyles.Normal;
        label.raycastTarget = false; label.enableAutoSizing = false; label.color = Ink; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; GameLocalizedText.Set(label,text); return label;
    }
```

### Assets/Scripts/NightShift/DeskOperationUI.cs

#### Text

원본:

```csharp
private static void Text(TextMeshProUGUI target, string value)
    {
        if (target != null && target.text != value)
        { target.text = value; }
    }
```

수정본:

```csharp
private static void Text(TextMeshProUGUI target, string value)
    {
        if (target != null && !GameLocalizedText.Matches(target,value))
        { GameLocalizedText.Set(target,value); }
    }
```

### Assets/Scripts/NightShift/DeviceTerminalUI.cs

#### Set

원본:

```csharp
public static void Set(TextMeshProUGUI target, string value)
    {
        if (target != null && target.text != value)
        { target.text = value; }
    }
```

수정본:

```csharp
public static void Set(TextMeshProUGUI target, string value)
    {
        if (target != null && !GameLocalizedText.Matches(target,value))
        { GameLocalizedText.Set(target,value); }
    }
```

#### QuickButton

원본:

```csharp
private Button QuickButton(string name,string caption,float x,float width)
    {
        var rect = new GameObject("Button :: Order " + name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(quickQuantityPanel.transform,false); Layout(rect,x,0,width,1);
        rect.gameObject.layer = quickQuantityPanel.layer;
        var image = rect.gameObject.AddComponent<Image>(); image.color = Neutral; image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Instantiate(reviewLabel,rect); label.name = "Text :: Order " + name;
        Layout(label.rectTransform,0,0,1,1); label.text = caption; label.fontSize = 29;
        label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false; label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; label.color = Ink;
        return button;
    }
```

수정본:

```csharp
private Button QuickButton(string name,string caption,float x,float width)
    {
        var rect = new GameObject("Button :: Order " + name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(quickQuantityPanel.transform,false); Layout(rect,x,0,width,1);
        rect.gameObject.layer = quickQuantityPanel.layer;
        var image = rect.gameObject.AddComponent<Image>(); image.color = Neutral; image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Instantiate(reviewLabel,rect); label.name = "Text :: Order " + name;
        Layout(label.rectTransform,0,0,1,1); GameLocalizedText.Set(label,caption); label.fontSize = 29;
        label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false; label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; label.color = Ink;
        return button;
    }
```

### Assets/Scripts/NightShift/InvestorJourneyNotes.cs

#### EnsureCopy

원본:

```csharp
private void EnsureCopy()
    {
        if (copy != null && loadedLanguage == language)
        { return; }
        string code;
        switch (language)
        {
            case StickyNoteLanguage.English: code = "en"; break;
            case StickyNoteLanguage.Korean: code = "ko"; break;
            case StickyNoteLanguage.Japanese: code = "ja"; break;
            default: throw new ArgumentOutOfRangeException(nameof(language));
        }
        var data = Resources.Load<TextAsset>("Localization/StickyNotes/" + code);
        if (data == null)
        { throw new InvalidOperationException("Missing sticky note language: " + code); }
        copy = JsonUtility.FromJson<Copy>(data.text); loadedLanguage = language; CurrentStage = -1;
    }
```

수정본:

```csharp
private void EnsureCopy()
    {
        if (GameLocale.Ready && localeRevision != GameLocale.Revision)
        {
            localeRevision = GameLocale.Revision;
            language = GameLocale.Code == "en" ? StickyNoteLanguage.English : GameLocale.Code == "ja" ? StickyNoteLanguage.Japanese : StickyNoteLanguage.Korean;
            copy = null;
        }
        if (copy != null && loadedLanguage == language)
        { return; }
        string code;
        switch (language)
        {
            case StickyNoteLanguage.English: code = "en"; break;
            case StickyNoteLanguage.Korean: code = "ko"; break;
            case StickyNoteLanguage.Japanese: code = "ja"; break;
            default: throw new ArgumentOutOfRangeException(nameof(language));
        }
        var data = Resources.Load<TextAsset>("Localization/StickyNotes/" + code);
        if (data == null)
        { throw new InvalidOperationException("Missing sticky note language: " + code); }
        copy = JsonUtility.FromJson<Copy>(data.text); loadedLanguage = language; CurrentStage = -1;
        if (GameLocale.Ready)
        {
            for (int i = 0; i < copy.titles.Length; i++) { copy.titles[i] = GameLocale.Get("notes.title." + i,copy.titles[i]); }
            foreach (var field in typeof(Copy).GetFields())
            { if (field.FieldType == typeof(string)) { field.SetValue(copy,GameLocale.Get("notes." + field.Name,(string)field.GetValue(copy))); } }
        }
        for (int i = 0; i < notes.Length; i++)
        {
            if ((completedMask & (1 << i)) == 0) { continue; }
            Set(notes[i].title,copy.titles[i]); Set(notes[i].detail,copy.complete); Set(notes[i].footer,""); Set(notes[i].status,copy.complete);
        }
    }
```

#### Set

원본:

```csharp
private static void Set(TMP_Text label, string value)
    {
        if (label != null && label.text != value)
        { label.text = value; }
    }
```

수정본:

```csharp
private static void Set(TMP_Text label, string value)
    {
        if (label != null && !GameLocalizedText.Matches(label,value))
        { GameLocalizedText.Set(label,value,true); }
    }
```

### Assets/Scripts/NightShift/LeadingRoomUI.cs

#### ConfigureRoomTabs

원본:

```csharp
private void ConfigureRoomTabs()
    {
        if (roomTwo != null) { roomTwo.gameObject.SetActive(false); }
        if (roomThree != null) { roomThree.gameObject.SetActive(false); }
        var tabs = npcRoom == null ? new[] { publicRoom, roomOne } : new[] { publicRoom, npcRoom, roomOne };
        float width = (.95f - .025f * (tabs.Length - 1)) / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            var rect = (RectTransform)tabs[i].transform;
            float x = .025f + i * (width + .025f);
            rect.anchorMin = new Vector2(x, rect.anchorMin.y); rect.anchorMax = new Vector2(x + width, rect.anchorMax.y);
            rect.offsetMin = new Vector2(0, rect.offsetMin.y); rect.offsetMax = new Vector2(0, rect.offsetMax.y);
        }
        var label = roomOne.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) { label.text = "내 리딩방"; }
    }
```

수정본:

```csharp
private void ConfigureRoomTabs()
    {
        if (roomTwo != null) { roomTwo.gameObject.SetActive(false); }
        if (roomThree != null) { roomThree.gameObject.SetActive(false); }
        var tabs = npcRoom == null ? new[] { publicRoom, roomOne } : new[] { publicRoom, npcRoom, roomOne };
        float width = (.95f - .025f * (tabs.Length - 1)) / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            var rect = (RectTransform)tabs[i].transform;
            float x = .025f + i * (width + .025f);
            rect.anchorMin = new Vector2(x, rect.anchorMin.y); rect.anchorMax = new Vector2(x + width, rect.anchorMax.y);
            rect.offsetMin = new Vector2(0, rect.offsetMin.y); rect.offsetMax = new Vector2(0, rect.offsetMax.y);
        }
        var label = roomOne.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) { GameLocalizedText.Set(label,"내 리딩방"); }
    }
```

#### Issue

원본:

```csharp
private void Issue()
    {
        if (!decimal.TryParse(buyPrice.text.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out decimal buy) ||
            !decimal.TryParse(sellPrice.text.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out decimal sell))
        { feedback.text = "소수점 없이 원 단위 가격을 입력하세요."; nextUpdate = Time.unscaledTime + 3f; return; }
        InvestorSociety.Instance?.Issue(chat.SelectedRoom, StockManager.Instance.CurrentStock, buy, sell);
    }
```

수정본:

```csharp
private void Issue()
    {
        if (!decimal.TryParse(buyPrice.text.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out decimal buy) ||
            !decimal.TryParse(sellPrice.text.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out decimal sell))
        { GameLocalizedText.Set(feedback,"소수점 없이 원 단위 가격을 입력하세요."); nextUpdate = Time.unscaledTime + 3f; return; }
        InvestorSociety.Instance?.Issue(chat.SelectedRoom, StockManager.Instance.CurrentStock, buy, sell);
    }
```

#### Set

원본:

```csharp
private static void Set(TextMeshProUGUI label, string text) { if (label.text != text) { label.text = text; } }
```

수정본:

```csharp
private static void Set(TextMeshProUGUI label, string text) { if (!GameLocalizedText.Matches(label,text)) { GameLocalizedText.Set(label,text); } }
```

### Assets/Scripts/NightShift/MarketEventFeed.cs

#### Update

원본:

```csharp
void Update()
    {
        if (eventText == null)
        { return; }
        bool visible = Time.unscaledTime < visibleUntil;
        if (eventText.enabled != visible) { eventText.enabled = visible; }
        if (dirty) { eventText.text = LatestEvent; dirty = false; }
    }
```

수정본:

```csharp
void Update()
    {
        if (eventText == null)
        { return; }
        bool visible = Time.unscaledTime < visibleUntil;
        if (eventText.enabled != visible) { eventText.enabled = visible; }
        if (dirty) { GameLocalizedText.Set(eventText,LatestEvent); dirty = false; }
    }
```

### Assets/Scripts/NightShift/PendingOrderListUI.cs

#### Configure

원본:

```csharp
public void Configure(DeviceOrderSession orders,TextMeshProUGUI fontTemplate,float fontScale=1f)
    {
        session=orders; template=fontTemplate; scale=fontScale; reading=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        title=Label("Pending Heading",transform,30,0,0,128,56,736,400); title.text="미체결";
        count=Label("Pending Order Count",transform,30,136,0,72,56,736,400); TradingOrderView.Numeric(count); count.alignment=TextAlignmentOptions.MidlineRight;
        Label("Pending Company Header",transform,24,0,64,270,32,736,400).text="미체결 종목";
        var price=Label("Pending Price Header",transform,24,286,64,170,32,736,400); price.text="가격 · 원"; price.alignment=TextAlignmentOptions.MidlineRight;
        var volume=Label("Pending Quantity Header",transform,24,472,64,144,32,736,400); volume.text="잔량 / 체결"; volume.alignment=TextAlignmentOptions.MidlineRight;
        var viewport=Rect("Area :: Pending Order Scroll",transform,0,104,736,296,736,400);
        var hit=viewport.gameObject.AddComponent<Image>(); hit.color=Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        content=Rect("Area :: Pending Order Rows",viewport,0,0,1,1,1,1);
        content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.content=content;
        scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=45f;
        empty=Label("No Pending Orders",viewport,28,0,0,1,.35f,1,1); empty.text="—"; empty.alignment=TextAlignmentOptions.Center;
        Refresh();
    }
```

수정본:

```csharp
public void Configure(DeviceOrderSession orders,TextMeshProUGUI fontTemplate,float fontScale=1f)
    {
        session=orders; template=fontTemplate; scale=fontScale; reading=Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        title=Label("Pending Heading",transform,30,0,0,128,56,736,400); GameLocalizedText.Set(title,"미체결");
        count=Label("Pending Order Count",transform,30,136,0,72,56,736,400); TradingOrderView.Numeric(count); count.alignment=TextAlignmentOptions.MidlineRight;
        GameLocalizedText.Set(Label("Pending Company Header",transform,24,0,64,270,32,736,400),"미체결 종목");
        var price=Label("Pending Price Header",transform,24,286,64,170,32,736,400); GameLocalizedText.Set(price,"가격 · 원"); price.alignment=TextAlignmentOptions.MidlineRight;
        var volume=Label("Pending Quantity Header",transform,24,472,64,144,32,736,400); GameLocalizedText.Set(volume,"잔량 / 체결"); volume.alignment=TextAlignmentOptions.MidlineRight;
        var viewport=Rect("Area :: Pending Order Scroll",transform,0,104,736,296,736,400);
        var hit=viewport.gameObject.AddComponent<Image>(); hit.color=Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        content=Rect("Area :: Pending Order Rows",viewport,0,0,1,1,1,1);
        content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.content=content;
        scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=45f;
        empty=Label("No Pending Orders",viewport,28,0,0,1,.35f,1,1); GameLocalizedText.Set(empty,"—"); empty.alignment=TextAlignmentOptions.Center;
        Refresh();
    }
```

#### CreateRow

원본:

```csharp
private Row CreateRow(int index)
    {
        var row=new Row {Root=Rect("Panel :: Pending Order "+index,content,0,0,1,1,1,1)};
        row.Root.anchorMin=new Vector2(0,1); row.Root.anchorMax=Vector2.one; row.Root.pivot=new Vector2(.5f,1);
        row.Root.sizeDelta=new Vector2(0,RowHeight*scale); row.Root.anchoredPosition=new Vector2(0,-index*RowHeight*scale);
        row.Side=Label("Pending Side",row.Root,24,0,8,56,32,736,88);
        row.Name=Label("Pending Stock",row.Root,26,68,8,202,32,736,88);
        row.Symbol=Label("Pending Symbol",row.Root,23,68,42,202,30,736,88); row.Symbol.color=new Color32(146,161,172,255);
        row.Price=Number("Pending Price",row.Root,286,8,170,32);
        row.Remaining=Number("Pending Remaining",row.Root,472,8,144,32);
        row.Filled=Number("Pending Filled",row.Root,472,42,144,30); row.Filled.fontSize=23*scale; row.Filled.color=row.Symbol.color;
        var divider=Rect("Image :: Pending Divider",row.Root,0,87,736,1,736,88).gameObject.AddComponent<Image>();
        divider.color=new Color32(44,60,69,255); divider.raycastTarget=false;
        var buttonRoot=Rect("Button :: Cancel Pending Order",row.Root,632,12,104,TradingOrderView.ControlHeight,736,88);
        var image=buttonRoot.gameObject.AddComponent<Image>(); var button=buttonRoot.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var label=Label("Cancel Pending Order",buttonRoot,26,0,0,1,1,1,1); label.alignment=TextAlignmentOptions.Center; label.text="취소";
        TabletControlStyle.Button(button); label.fontStyle=FontStyles.Normal;
        button.onClick.AddListener(()=>{session.CancelPending(row.Entry); Refresh();}); return row;
    }
```

수정본:

```csharp
private Row CreateRow(int index)
    {
        var row=new Row {Root=Rect("Panel :: Pending Order "+index,content,0,0,1,1,1,1)};
        row.Root.anchorMin=new Vector2(0,1); row.Root.anchorMax=Vector2.one; row.Root.pivot=new Vector2(.5f,1);
        row.Root.sizeDelta=new Vector2(0,RowHeight*scale); row.Root.anchoredPosition=new Vector2(0,-index*RowHeight*scale);
        row.Side=Label("Pending Side",row.Root,24,0,8,56,32,736,88);
        row.Name=Label("Pending Stock",row.Root,26,68,8,202,32,736,88);
        row.Symbol=Label("Pending Symbol",row.Root,23,68,42,202,30,736,88); row.Symbol.color=new Color32(146,161,172,255);
        row.Price=Number("Pending Price",row.Root,286,8,170,32);
        row.Remaining=Number("Pending Remaining",row.Root,472,8,144,32);
        row.Filled=Number("Pending Filled",row.Root,472,42,144,30); row.Filled.fontSize=23*scale; row.Filled.color=row.Symbol.color;
        var divider=Rect("Image :: Pending Divider",row.Root,0,87,736,1,736,88).gameObject.AddComponent<Image>();
        divider.color=new Color32(44,60,69,255); divider.raycastTarget=false;
        var buttonRoot=Rect("Button :: Cancel Pending Order",row.Root,632,12,104,TradingOrderView.ControlHeight,736,88);
        var image=buttonRoot.gameObject.AddComponent<Image>(); var button=buttonRoot.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var label=Label("Cancel Pending Order",buttonRoot,26,0,0,1,1,1,1); label.alignment=TextAlignmentOptions.Center; GameLocalizedText.Set(label,"취소");
        TabletControlStyle.Button(button); label.fontStyle=FontStyles.Normal;
        button.onClick.AddListener(()=>{session.CancelPending(row.Entry); Refresh();}); return row;
    }
```

#### Label

원본:

```csharp
private TextMeshProUGUI Label(string name,Transform parent,float size,float x,float y,float w,float h,float pw,float ph)
    {
        var label=Instantiate(template,parent); label.name="Text :: "+name;
        if(reading!=null) { label.font=reading; label.fontSharedMaterial=reading.material; }
        TradingOrderView.Place(label.rectTransform,parent,x,y,w,h,pw,ph);
        label.fontSize=size*scale; label.enableAutoSizing=false; label.fontStyle=FontStyles.Normal; label.margin=Vector4.zero;
        label.textWrappingMode=TextWrappingModes.NoWrap; label.overflowMode=TextOverflowModes.Ellipsis;
        label.alignment=TextAlignmentOptions.MidlineLeft; label.raycastTarget=false; label.color=new Color32(230,235,239,255); label.text=""; return label;
    }
```

수정본:

```csharp
private TextMeshProUGUI Label(string name,Transform parent,float size,float x,float y,float w,float h,float pw,float ph)
    {
        var label=Instantiate(template,parent); label.name="Text :: "+name;
        if(reading!=null) { label.font=reading; label.fontSharedMaterial=reading.material; }
        TradingOrderView.Place(label.rectTransform,parent,x,y,w,h,pw,ph);
        label.fontSize=size*scale; label.enableAutoSizing=false; label.fontStyle=FontStyles.Normal; label.margin=Vector4.zero;
        label.textWrappingMode=TextWrappingModes.NoWrap; label.overflowMode=TextOverflowModes.Ellipsis;
        label.alignment=TextAlignmentOptions.MidlineLeft; label.raycastTarget=false; label.color=new Color32(230,235,239,255); GameLocalizedText.Set(label,""); return label;
    }
```

### Assets/Scripts/NightShift/PhoneCallController.cs

#### Update

원본:

```csharp
void Update()
    {
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed)
        { return; }
        if (DeskViewController.Instance == null && !MouseCameraController.TextEntryActive && Input.GetKeyDown(KeyCode.Tab) && !TabletViewController.IsTabletHeld)
        { if (raised) { Lower(); } else { Raise(); } }
        if (raised && Input.GetKeyDown(KeyCode.Escape)) { Lower(); }
        if (MarketDayController.CanTrade)
        {
            if (!Ringing && Time.time >= nextCall) { Ring((Caller)random.Next(3)); }
            if (Ringing && Time.time >= expires) { Decline(); message = "부재중 전화. 같은 기회가 다시 오지는 않습니다."; }
        }
        if (Time.unscaledTime < nextUi)
        { return; }
        nextUi = Time.unscaledTime + .25f;
        if (screenUI != null)
        { screenUI.Refresh(this); }
        else
        {
            title.text = Ringing ? (CurrentCaller == Caller.Institution ? "발신자: 기관 담당자 (자칭)" : CurrentCaller == Caller.Research ? "발신자: 리서치 데스크" : "발신자: 알 수 없는 번호") : "연락 기록";
            body.text = Ringing ? "전화를 받으시겠습니까?\n발신자의 주장과 실제 체결 흐름은 다를 수 있습니다.\n\n" + RemainingSeconds + "초 후 종료" : message;
        }
        answer.interactable = decline.interactable = Ringing && MarketDayController.CanTrade;
    }
```

수정본:

```csharp
void Update()
    {
        if (StockManager.Instance == null || !StockManager.Instance.SetupConfirmed)
        { return; }
        if (DeskViewController.Instance == null && !MouseCameraController.TextEntryActive && Input.GetKeyDown(KeyCode.Tab) && !TabletViewController.IsTabletHeld)
        { if (raised) { Lower(); } else { Raise(); } }
        if (raised && Input.GetKeyDown(KeyCode.Escape)) { Lower(); }
        if (MarketDayController.CanTrade)
        {
            if (!Ringing && Time.time >= nextCall) { Ring((Caller)random.Next(3)); }
            if (Ringing && Time.time >= expires) { Decline(); message = "부재중 전화. 같은 기회가 다시 오지는 않습니다."; }
        }
        if (Time.unscaledTime < nextUi)
        { return; }
        nextUi = Time.unscaledTime + .25f;
        if (screenUI != null)
        { screenUI.Refresh(this); }
        else
        {
            GameLocalizedText.Set(title,Ringing ? (CurrentCaller == Caller.Institution ? "발신자: 기관 담당자 (자칭)" : CurrentCaller == Caller.Research ? "발신자: 리서치 데스크" : "발신자: 알 수 없는 번호") : "연락 기록");
            GameLocalizedText.Set(body,Ringing ? "전화를 받으시겠습니까?\n발신자의 주장과 실제 체결 흐름은 다를 수 있습니다.\n\n" + RemainingSeconds + "초 후 종료" : message);
        }
        answer.interactable = decline.interactable = Ringing && MarketDayController.CanTrade;
    }
```

### Assets/Scripts/NightShift/SpreadTradingUI.cs

#### Install

원본:

```csharp
public static SpreadTradingUI Install(TabletAccountUI tablet)
    {
        if (!tablet.second || tablet.orders == null || tablet.orders.legacy == null)
        { return null; }
        var legacy = tablet.orders.legacy;
        var root = (RectTransform)legacy.deskPanel.transform;
        var model = (RectTransform)tablet.orders.panel.transform;
        root.anchorMin = model.anchorMin; root.anchorMax = model.anchorMax; root.pivot = model.pivot;
        root.offsetMin = model.offsetMin; root.offsetMax = model.offsetMax;
        foreach (Transform child in root)
        { child.gameObject.SetActive(false); }
        var view = root.gameObject.AddComponent<SpreadTradingUI>();
        legacy.spreadView = view;
        view.tablet = tablet; view.operation = TradingOperationController.Instance;
        if (view.operation != null)
        { view.operation.account = tablet.account; view.operation.spreadSession = tablet.orders.session; }
        view.Build();
        var label = tablet.autoButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) { label.text = "스프레드"; }
        return view;
    }
```

수정본:

```csharp
public static SpreadTradingUI Install(TabletAccountUI tablet)
    {
        if (!tablet.second || tablet.orders == null || tablet.orders.legacy == null)
        { return null; }
        var legacy = tablet.orders.legacy;
        var root = (RectTransform)legacy.deskPanel.transform;
        var model = (RectTransform)tablet.orders.panel.transform;
        root.anchorMin = model.anchorMin; root.anchorMax = model.anchorMax; root.pivot = model.pivot;
        root.offsetMin = model.offsetMin; root.offsetMax = model.offsetMax;
        foreach (Transform child in root)
        { child.gameObject.SetActive(false); }
        var view = root.gameObject.AddComponent<SpreadTradingUI>();
        legacy.spreadView = view;
        view.tablet = tablet; view.operation = TradingOperationController.Instance;
        if (view.operation != null)
        { view.operation.account = tablet.account; view.operation.spreadSession = tablet.orders.session; }
        view.Build();
        var label = tablet.autoButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) { GameLocalizedText.Set(label,"스프레드"); }
        return view;
    }
```

#### Label

원본:

```csharp
private TextMeshProUGUI Label(string name,string value,float x,float y,float w,float h,float size)
    {
        var obj = new GameObject("Text :: " + name,typeof(RectTransform),typeof(TextMeshProUGUI)); obj.layer = gameObject.layer;
        var label = obj.GetComponent<TextMeshProUGUI>(); TradingOrderView.Place(label.rectTransform,transform,x,y,w,h,1600,880);
        label.font = font; label.fontSize = size*scale; label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false;
        label.color = Ink; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.text = value; return label;
    }
```

수정본:

```csharp
private TextMeshProUGUI Label(string name,string value,float x,float y,float w,float h,float size)
    {
        var obj = new GameObject("Text :: " + name,typeof(RectTransform),typeof(TextMeshProUGUI)); obj.layer = gameObject.layer;
        var label = obj.GetComponent<TextMeshProUGUI>(); TradingOrderView.Place(label.rectTransform,transform,x,y,w,h,1600,880);
        label.font = font; label.fontSize = size*scale; label.fontStyle = FontStyles.Normal; label.enableAutoSizing = false;
        label.color = Ink; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.MidlineLeft; GameLocalizedText.Set(label,value); return label;
    }
```

### Assets/Scripts/NightShift/TradingOrderView.cs

#### Action

원본:

```csharp
private void Action(Button button,string caption,bool primary=false)
    {
        TabletControlStyle.Button(button,primary);
        var label=button.GetComponentInChildren<TextMeshProUGUI>(true);
        TextStyle(label,28,false); label.fontStyle=FontStyles.Normal; label.alignment=TextAlignmentOptions.Center; label.text=caption;
    }
```

수정본:

```csharp
private void Action(Button button,string caption,bool primary=false)
    {
        TabletControlStyle.Button(button,primary);
        var label=button.GetComponentInChildren<TextMeshProUGUI>(true);
        TextStyle(label,28,false); label.fontStyle=FontStyles.Normal; label.alignment=TextAlignmentOptions.Center; GameLocalizedText.Set(label,caption);
    }
```

#### Label

원본:

```csharp
private TextMeshProUGUI Label(string name,Transform parent,string text,float x,float y,float w,float h,float size,float pw,float ph)
    {
        var label=new GameObject("Text :: "+name,typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        Place(label.rectTransform,parent,x,y,w,h,pw,ph); TextStyle(label,size,false); label.alignment=TextAlignmentOptions.MidlineLeft; label.text=text; return label;
    }
```

수정본:

```csharp
private TextMeshProUGUI Label(string name,Transform parent,string text,float x,float y,float w,float h,float size,float pw,float ph)
    {
        var label=new GameObject("Text :: "+name,typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        Place(label.rectTransform,parent,x,y,w,h,pw,ph); TextStyle(label,size,false); label.alignment=TextAlignmentOptions.MidlineLeft; GameLocalizedText.Set(label,text); return label;
    }
```

### Assets/Scripts/OrderBookUI/DrawRawOrder.cs

#### SetData

원본:

```csharp
public void SetData(decimal price, decimal displayValue, decimal maxValue, decimal lastPrice, bool isTurnover)
    {
        currentPrice = price;

        if (displayValue <= 0m)
        {
            Clear();
            return;
        }

        txtPrice.text = PriceTickPolicy.FormatPrice(price);
        
        if (isTurnover)
        {
            txtVolume.text = FormatTurnover(displayValue, price);
        }
        else
        {
            txtVolume.text = ((long)displayValue).ToString("#,##0");
        }

        float percent = maxValue <= 0m ? 0f : (float)(displayValue / maxValue);
        volumeGuage.anchorMin = new Vector2(0f, 0f);
        volumeGuage.anchorMax = new Vector2(percent, 1f);

        bool isLastPrice = price == lastPrice;
        highlight.SetActive(isLastPrice);
    }
```

수정본:

```csharp
public void SetData(decimal price, decimal displayValue, decimal maxValue, decimal lastPrice, bool isTurnover)
    {
        currentPrice = price;

        if (displayValue <= 0m)
        {
            Clear();
            return;
        }

        GameLocalizedText.Set(txtPrice,PriceTickPolicy.FormatPrice(price));
        
        if (isTurnover)
        {
            GameLocalizedText.Set(txtVolume,FormatTurnover(displayValue, price));
        }
        else
        {
            GameLocalizedText.Set(txtVolume,((long)displayValue).ToString("#,##0"));
        }

        float percent = maxValue <= 0m ? 0f : (float)(displayValue / maxValue);
        volumeGuage.anchorMin = new Vector2(0f, 0f);
        volumeGuage.anchorMax = new Vector2(percent, 1f);

        bool isLastPrice = price == lastPrice;
        highlight.SetActive(isLastPrice);
    }
```

#### Clear

원본:

```csharp
public void Clear()
    {
        txtPrice.text = "";
        txtVolume.text = "";
        volumeGuage.anchorMax = new Vector2(0f, 1f);
        highlight.SetActive(false);
    }
```

수정본:

```csharp
public void Clear()
    {
        GameLocalizedText.Set(txtPrice,"");
        GameLocalizedText.Set(txtVolume,"");
        volumeGuage.anchorMax = new Vector2(0f, 1f);
        highlight.SetActive(false);
    }
```

### Assets/Scripts/OrderBookUI/OrderBookUIManager.cs

#### UpdateToggleButtonText

원본:

```csharp
void UpdateToggleButtonText()
    {
        if (toggleButtonText != null)
        {
            toggleButtonText.text = _displayMode == VolumeDisplayMode.Volume ? "거래량 보기" : "거래대금 보기";
        }
    }
```

수정본:

```csharp
void UpdateToggleButtonText()
    {
        if (toggleButtonText != null)
        {
            GameLocalizedText.Set(toggleButtonText,_displayMode == VolumeDisplayMode.Volume ? "거래량 보기" : "거래대금 보기");
        }
    }
```

### Assets/Scripts/StockListUI/StockListItem.cs

#### Set

원본:

```csharp
public void Set(Stock stock, IReadOnlyList<CandleData> last10)
    {
        if (stock == null)
        { return; }

        _stock = stock;
        if (icon != null && iconSymbol != stock.symbol)
        {
            iconSymbol = stock.symbol;
            var sprite = Resources.Load<Sprite>(IconPath + stock.symbol);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (textSymbol != null)
        {
            DeviceTerminalUI.Set(textSymbol,stock.symbol);
        }

        if (textName != null)
        {
            string name = textRisk != null ? stock.stockName : stock.stockName + " · " + stock.RiskDisplay;
            if (textName.text != name)
            {
                textName.text = name;
                badgeLayoutDirty = true;
            }
        }

        if (textPrice != null)
        {
            DeviceTerminalUI.Set(textPrice,PriceTickPolicy.FormatPrice(stock.currentPrice));
        }

        ApplyRiskBadge(stock);
        LayoutBadge();
        bool nextSelected = StockManager.Instance != null && StockManager.Instance.CurrentStock == stock;
        if (!selectionInitialized || selected != nextSelected)
        {
            selected = nextSelected;
            selectionInitialized = true;
            if (selectionHighlight != null)
            {
                selectionHighlight.enabled = selected;
            }
            else if (rowBackground != null)
            {
                rowBackground.sprite = selected ? selectedBackground : normalBackground;
            }
        }

        // 거래량/거래대금: 최근 18캔들 전체 합
        // 미니 캔들은 당일 OHLC. 등락률/등락률 정렬은 고정된 전일 종가 기준.
        float volumeSum = 0f;
        float turnoverSum = 0f;
        decimal dailyChange = 0m;
        var statistics = StockStatistics.Instance;
        bool hasDailyChange = statistics != null && statistics.TryGetDailyChange(stock, out dailyChange);
        float changePct = (float)dailyChange;
        CandleData? summary = null;
        if (statistics != null && statistics.TryGetDailyCandle(stock, out var dailyCandle))
        {
            summary = dailyCandle;
        }
        int nAll = last10 != null ? last10.Count : 0;
        if (nAll > 0)
        {
            for (int i = 0; i < nAll; i++)
            {
                volumeSum += last10[i].volume;
                turnoverSum += last10[i].close * last10[i].volume;
            }
        }

        if (textVolume10 != null)
        {
            DeviceTerminalUI.Set(textVolume10,FormatVolume(volumeSum) + "주");
        }

        if (textTurnover != null)
        {
            DeviceTerminalUI.Set(textTurnover,"거래대금(추정) " + FormatTurnover(turnoverSum) + "원");
        }

        if (textChangePct10 != null)
        {
            DeviceTerminalUI.Set(textChangePct10,hasDailyChange ? (changePct > 0 ? $"+{changePct:F1}%" : $"{changePct:F1}%") : "—");
            textChangePct10.color = changePct > 0 ? Up : changePct < 0 ? Down : new Color32(138,150,163,255);
        }
        VolumeSortValue = volumeSum; ChangeSortValue = changePct;
        ApplyCandleAnchors(summary);
    }
```

수정본:

```csharp
public void Set(Stock stock, IReadOnlyList<CandleData> last10)
    {
        if (stock == null)
        { return; }

        _stock = stock;
        if (icon != null && iconSymbol != stock.symbol)
        {
            iconSymbol = stock.symbol;
            var sprite = Resources.Load<Sprite>(IconPath + stock.symbol);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (textSymbol != null)
        {
            DeviceTerminalUI.Set(textSymbol,stock.symbol);
        }

        if (textName != null)
        {
            string name = textRisk != null ? stock.stockName : stock.stockName + " · " + stock.RiskDisplay;
            if (!GameLocalizedText.Matches(textName,name))
            {
                GameLocalizedText.Set(textName,name);
                badgeLayoutDirty = true;
            }
        }

        if (textPrice != null)
        {
            DeviceTerminalUI.Set(textPrice,PriceTickPolicy.FormatPrice(stock.currentPrice));
        }

        ApplyRiskBadge(stock);
        LayoutBadge();
        bool nextSelected = StockManager.Instance != null && StockManager.Instance.CurrentStock == stock;
        if (!selectionInitialized || selected != nextSelected)
        {
            selected = nextSelected;
            selectionInitialized = true;
            if (selectionHighlight != null)
            {
                selectionHighlight.enabled = selected;
            }
            else if (rowBackground != null)
            {
                rowBackground.sprite = selected ? selectedBackground : normalBackground;
            }
        }

        // 거래량/거래대금: 최근 18캔들 전체 합
        // 미니 캔들은 당일 OHLC. 등락률/등락률 정렬은 고정된 전일 종가 기준.
        float volumeSum = 0f;
        float turnoverSum = 0f;
        decimal dailyChange = 0m;
        var statistics = StockStatistics.Instance;
        bool hasDailyChange = statistics != null && statistics.TryGetDailyChange(stock, out dailyChange);
        float changePct = (float)dailyChange;
        CandleData? summary = null;
        if (statistics != null && statistics.TryGetDailyCandle(stock, out var dailyCandle))
        {
            summary = dailyCandle;
        }
        int nAll = last10 != null ? last10.Count : 0;
        if (nAll > 0)
        {
            for (int i = 0; i < nAll; i++)
            {
                volumeSum += last10[i].volume;
                turnoverSum += last10[i].close * last10[i].volume;
            }
        }

        if (textVolume10 != null)
        {
            DeviceTerminalUI.Set(textVolume10,FormatVolume(volumeSum) + "주");
        }

        if (textTurnover != null)
        {
            DeviceTerminalUI.Set(textTurnover,"거래대금(추정) " + FormatTurnover(turnoverSum) + "원");
        }

        if (textChangePct10 != null)
        {
            DeviceTerminalUI.Set(textChangePct10,hasDailyChange ? (changePct > 0 ? $"+{changePct:F1}%" : $"{changePct:F1}%") : "—");
            textChangePct10.color = changePct > 0 ? Up : changePct < 0 ? Down : new Color32(138,150,163,255);
        }
        VolumeSortValue = volumeSum; ChangeSortValue = changePct;
        ApplyCandleAnchors(summary);
    }
```

#### ApplyRiskBadge

원본:

```csharp
void ApplyRiskBadge(Stock stock)
    {
        Color color = stock.RiskPending ? new Color32(157, 166, 175, 255) : stock.RiskGrade switch
        {
            StockRiskGrade.Safe => new Color32(133, 182, 151, 255),
            StockRiskGrade.Warning => new Color32(218, 195, 111, 255),
            StockRiskGrade.Danger => new Color32(225, 160, 104, 255),
            StockRiskGrade.HighRisk => new Color32(228, 127, 135, 255),
            _ => new Color32(157, 166, 175, 255)
        };
        if (textRisk != null)
        {
            string label = stock.RiskLabel;
            if (stock.IsTradingHalted)
            {
                label += " " + Mathf.CeilToInt(Mathf.Max(0f, stock.HaltUntil - Time.time)) + "초";
            }
            if (textRisk.text != label)
            {
                textRisk.text = label;
                badgeLayoutDirty = true;
            }
            if (textRisk.color != color)
            {
                textRisk.color = color;
            }
        }
        if (riskBadgeBackground != null)
        {
            color.a = .12f;
            if (riskBadgeBackground.color != color)
            {
                riskBadgeBackground.color = color;
            }
        }
    }
```

수정본:

```csharp
void ApplyRiskBadge(Stock stock)
    {
        Color color = stock.RiskPending ? new Color32(157, 166, 175, 255) : stock.RiskGrade switch
        {
            StockRiskGrade.Safe => new Color32(133, 182, 151, 255),
            StockRiskGrade.Warning => new Color32(218, 195, 111, 255),
            StockRiskGrade.Danger => new Color32(225, 160, 104, 255),
            StockRiskGrade.HighRisk => new Color32(228, 127, 135, 255),
            _ => new Color32(157, 166, 175, 255)
        };
        if (textRisk != null)
        {
            string label = stock.RiskLabel;
            if (stock.IsTradingHalted)
            {
                label += " " + Mathf.CeilToInt(Mathf.Max(0f, stock.HaltUntil - Time.time)) + "초";
            }
            if (!GameLocalizedText.Matches(textRisk,label))
            {
                GameLocalizedText.Set(textRisk,label);
                badgeLayoutDirty = true;
            }
            if (textRisk.color != color)
            {
                textRisk.color = color;
            }
        }
        if (riskBadgeBackground != null)
        {
            color.a = .12f;
            if (riskBadgeBackground.color != color)
            {
                riskBadgeBackground.color = color;
            }
        }
    }
```

### Assets/Scripts/StockListUI/StockListUIManager.cs

#### SortButton

원본:

```csharp
private static Button SortButton(Transform parent,TextMeshProUGUI source,string name,string caption,float x,float width,out TextMeshProUGUI label)
    {
        var rect = new GameObject("Button :: Sort " + name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = new Vector2(x,0); rect.anchorMax = new Vector2(x+width,1); rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(23,31,37,255); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        label = Instantiate(source,rect); label.name = "Text :: Sort " + name; label.gameObject.SetActive(true);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.text = caption; label.fontSize = 44; label.enableAutoSizing = false; label.fontStyle = FontStyles.Normal;
        label.color = new Color32(230,235,239,255); label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return button;
    }
```

수정본:

```csharp
private static Button SortButton(Transform parent,TextMeshProUGUI source,string name,string caption,float x,float width,out TextMeshProUGUI label)
    {
        var rect = new GameObject("Button :: Sort " + name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = new Vector2(x,0); rect.anchorMax = new Vector2(x+width,1); rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(23,31,37,255); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        label = Instantiate(source,rect); label.name = "Text :: Sort " + name; label.gameObject.SetActive(true);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        GameLocalizedText.Set(label,caption); label.fontSize = 44; label.enableAutoSizing = false; label.fontStyle = FontStyles.Normal;
        label.color = new Color32(230,235,239,255); label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return button;
    }
```

