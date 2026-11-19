using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// A versioned, local whole-session checkpoint. Shared stocks/orders remain shared; decimal money stays exact.
/// Only explicitly listed simulation components are roots. No UI hierarchy, callbacks, native handles or settings are serialized.
/// Reflection is used only on explicit save/load, never on a simulation tick. Unknown schemas/types fail closed.
/// </summary>
public static partial class SessionStore
{
    private const int Schema = 2, MaxNodes = 500000, MaxBytes = 128*1024*1024;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly HashSet<string> RootNames = new(StringComparer.Ordinal)
    {
        "StockManager","StockStatistics","CandleHistoryManager","ExchangeWarehouse","MarketDayController","InvestorSociety","CorporateEconomy",
        "PlayerProgression","PlayerAccounts","DailyIncomeController","FakeNewsManager","NewsManager","ItCompanyManager",
        "StockNewsAttentionManager","StockInterestManager","MarketSentiment","DelistingManager","PsychologicalLevelManager",
        "LiquidityBotManager","TradeBotManager","InstitutionBootstrapper","WorldDramaDirector","MarketRhythmDirector",
        "TradingOperationController","DeviceOrderSession","DeviceMarketJournal","NpcLeadingRoom","CompanyNewsAudience","SocialContactController","PhoneCallController","IncumbentExchangeAI","PlayerRestController"
    };
    public static bool ValidationMode => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT"));
    private static string Folder => ValidationMode ? Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT") : Application.persistentDataPath;
    /// <summary>압축·암호화·서명된 바이너리 체크포인트.</summary>
    public static string PathName => Path.Combine(Folder,"session-v3.dat");
    /// <summary>이전 버전의 JSON 텍스트 저장. 읽기만 한다.</summary>
    public static string LegacyPathName => Path.Combine(Folder,"session-v2.json");
    public static bool Exists => File.Exists(PathName) || File.Exists(LegacyPathName);
    public static bool CanSave => StockManager.Instance != null && StockManager.Instance.SetupConfirmed && MarketDayController.Instance != null &&
        MarketDayController.Instance.CheckpointReady && !MarketDayController.Instance.IntegrityFailed &&
        !StockManager.Instance.stocks.Any(stock=>stock.IsDelisting);
    private static bool Root(Component value) => value is Trader || RootNames.Contains(value.GetType().Name);
    /// <summary>게임 이름을 바꾸기 전의 제품 이름. 저장 폴더가 이 이름으로 만들어져 있었다.</summary>
    private const string FormerProductName = "DonDon";
    /// <summary>
    /// 제품 이름이 바뀌면 저장 폴더도 바뀐다. 새 폴더에 저장이 없고 예전 폴더에 있으면 한 번 복사해 온다.
    /// 예전 폴더는 지우지 않는다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CarryOverFormerSaves()
    {
        if (ValidationMode || Exists) { return; }
        try
        {
            var parent = Directory.GetParent(Application.persistentDataPath);
            if (parent == null) { return; }
            string former = Path.Combine(parent.FullName, FormerProductName);
            if (!Directory.Exists(former) || Path.GetFullPath(former) == Path.GetFullPath(Application.persistentDataPath)) { return; }
            Directory.CreateDirectory(Application.persistentDataPath);
            foreach (string name in new[] { "session-v3.dat", "session-v2.json" })
            {
                string source = Path.Combine(former, name), target = Path.Combine(Application.persistentDataPath, name);
                if (File.Exists(source) && !File.Exists(target)) { File.Copy(source, target); Debug.Log("이전 저장을 새 폴더로 가져왔습니다: " + name); }
            }
        }
        catch (Exception error) { Debug.LogWarning("이전 저장을 가져오지 못했습니다: " + error.Message); }
    }
    /// <summary>동기 저장. 검증 도구용이며 게임의 마감 저장은 BeginSave의 비동기 경로를 쓴다.</summary>
    public static void Save()
    {
        var job=BeginSave();
        job.CompleteCaptureNow(); job.Wait();
        if (job.Error!=null) { throw job.Error; }
    }
    public static string Read()
    {
        WaitForPendingSave();
        if (File.Exists(PathName)) { VerifyBinary(PathName); return BinaryMarker+PathName; }
        if (new FileInfo(LegacyPathName).Length > MaxBytes*2L) { throw new InvalidDataException("Checkpoint exceeds storage limit."); }
        string text=File.ReadAllText(LegacyPathName); Verify(text); return text;
    }
    private static string Verify(string text)
    {
        using var reader = new JsonTextReader(new StringReader(text)) { MaxDepth = 128, DateParseHandling = DateParseHandling.None };
        var envelope=JObject.Load(reader);
        string payload=(string)envelope["payload"];
        if ((int?)envelope["schema"] != Schema || payload == null || payload.Length > MaxBytes || (string)envelope["sha256"] != Hash(Encoding.UTF8.GetBytes(payload)))
        { throw new InvalidDataException("Checkpoint is damaged or incompatible."); }
        return payload;
    }
    private static JObject Decode(string text)
    {
        // 바이너리 저장은 큰 문자열을 만들지 않고 파일에서 바로 읽는다.
        var data=text!=null && text.StartsWith(BinaryMarker,StringComparison.Ordinal) ? LoadBinary(text.Substring(BinaryMarker.Length)) : JObject.Parse(Verify(text));
        if ((int?)data["schema"] != Schema || (string)data["scene"] != "TradeScene" || data["nodes"] is not JArray nodes || nodes.Count > MaxNodes)
        { throw new InvalidDataException("Invalid checkpoint schema."); }
        return data;
    }
    private static string Hash(byte[] bytes) { using var sha=SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(bytes)); }
    public static void Restore(string text)
    {
        if (MarketDayController.Instance == null || !MarketDayController.Instance.Ready) { throw new InvalidOperationException("World is not ready."); }
        RestoreData(Decode(text));
    }
    // A saved market may contain newcomers absent from a fresh scene. Let their normal Start lifecycle
    // finish while the world is paused, then restore balances/psychology over that initialized shell.
    public static IEnumerator RestoreInitialized(string text,Action<Exception> complete)
    {
        JObject data=null; bool created=false; Exception error=null;
        try
        {
            data=Decode(text);
            var nodes=(JArray)data["nodes"]; var types=(JArray)data["types"];
            var existing=new HashSet<string>(Object.FindObjectsByType<Trader>(FindObjectsInactive.Include).Select(Key),StringComparer.Ordinal);
            var idField=typeof(Trader).GetField("<InitialAccountId>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var binding in ((JArray)data["roots"]).OrderBy(b=> ParseTraderId((string)b["key"])))
            {
                string key=(string)binding["key"];
                if (!key.StartsWith("trader:",StringComparison.Ordinal) || existing.Contains(key)) { continue; }
                var node=nodes[(int)binding["value"]["r"]]; Type type=Type.GetType((string)types[(int)node["t"]],false);
                if (type==null || !typeof(TradeBot).IsAssignableFrom(type) || type.IsAbstract || type==typeof(ExchangeBot))
                { throw new InvalidDataException("Cannot recreate checkpoint participant: "+key); }
                var go=new GameObject(type.Name+"_Restored"); var trader=(Trader)go.AddComponent(type);
                idField.SetValue(trader,ParseTraderId(key)); existing.Add(key); created=true;
            }
        }
        catch(Exception ex) { error=ex; }
        if (error!=null) { complete(error); yield break; }
        if (created) { yield return null; yield return null; }
        try { RestoreData(data); }
        catch(Exception ex) { error=ex; }
        complete(error);
    }
    private static int ParseTraderId(string key)
    { return key.StartsWith("trader:",StringComparison.Ordinal) && int.TryParse(key.Split(':')[1],out int id) ? id : 0; }
    private static void RestoreData(JObject data)
    {
        MarketDayController.Instance.PrepareSessionRestore();
        var graph=new Graph();
        graph.Restore(data);
        typeof(Trader).GetField("nextInitialAccountId",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Trader.Registry.Max(t=>t.InitialAccountId));
        SessionClock.Restore((float)data["clock"]);
        UnityEngine.Random.state=JsonUtility.FromJson<UnityEngine.Random.State>((string)data["random"]);
        FakeNewsManager.Instance?.RestorePlayerCooldown((double)data["newsCooldown"],(long)data["savedUtc"]);
        typeof(ChatSocialIds).GetField("last",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,long.Parse((string)data["socialId"],Culture));
        InvestorSociety.Instance?.ReconnectSession();
        var day=MarketDayController.Instance;
        CorporateEconomy.Instance?.ReconnectSession();
        StockManager.Instance.ReconnectListedCompanies();
        day.ConversationFocus.GameMinutes=()=> (day.Day-1)*1440d+day.GameMinute;
        foreach (var session in Object.FindObjectsByType<DeviceOrderSession>(FindObjectsInactive.Include)) { session.ReconnectSession(); }
        foreach (var operation in Object.FindObjectsByType<TradingOperationController>(FindObjectsInactive.Include)) { operation.ReconnectSession(); }
        foreach (var journal in Object.FindObjectsByType<DeviceMarketJournal>(FindObjectsInactive.Include))
        {
            if (!((JArray)data["roots"]).Any(binding=>(string)binding["key"]==Key(journal))) { journal.ClearHistory(); }
            journal.ReconnectSession();
        }
        var roots=(JArray)data["roots"];
        var components=Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(Root).ToDictionary(Key,StringComparer.Ordinal);
        foreach (var binding in roots)
        {
            var component=components[(string)binding["key"]];
            component.enabled=(bool)binding["enabled"];
            if (component.gameObject.activeSelf!=(bool)binding["active"]) { component.gameObject.SetActive((bool)binding["active"]); }
        }
        GameLocale.ProtectLiteral(DailyIncomeController.Instance?.CompanyName);
        foreach (var bot in MarketDayController.Instance.Bots) { if (bot?.Social != null) { foreach (string name in bot.Social.Names) { GameLocale.ProtectLiteral(name); } } }
        foreach (var list in Object.FindObjectsByType<StockListUIManager>(FindObjectsInactive.Include)) { list.ReconnectSession(); }
        var market=StockManager.Instance; int selected=market.stocks.IndexOf(market.CurrentStock);
        market.CurrentStock=null;
        if (market.stocks.Count>0) { market.SelectStock(Math.Max(0,selected)); }
    }
    private static string Key(Object value)
    {
        if (value is Trader trader) { return "trader:"+trader.InitialAccountId+":"+trader.GetType().FullName; }
        if (value is Component component)
        {
            string path=component.name; var parent=component.transform.parent;
            while (parent != null) { path=parent.name+"/"+path; parent=parent.parent; }
            int index=Array.IndexOf(component.GetComponents(component.GetType()),component);
            return "component:"+component.GetType().FullName+":"+path+":"+index;
        }
        return "asset:"+value.GetType().FullName+":"+value.name;
    }
    private sealed class ReferenceComparer : IEqualityComparer<object>
    { public new bool Equals(object a,object b) => ReferenceEquals(a,b); public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value); }
    private sealed class Graph
    {
        public readonly JArray Nodes = new();
        public readonly JArray Types = new();
        private readonly Dictionary<Type,int> typeIds = new();
        private readonly Dictionary<object,int> ids = new(new ReferenceComparer());
        private readonly Dictionary<string,Object> unity = new(StringComparer.Ordinal);
        private readonly Dictionary<string,Stock> stocks = new(StringComparer.Ordinal);
        private object[] restored;
        private JArray saved;
        private readonly Dictionary<Type,FieldInfo[]> fields = new();
        public Graph()
        {
            foreach(var component in Object.FindObjectsByType<Component>(FindObjectsInactive.Include)) { unity[Key(component)]=component; }
            foreach(var stock in StockManager.Instance.stocks.Concat(StockManager.Instance.UnlistedStocks)) { stocks[stock.symbol]=stock; }
        }
        private FieldInfo[] Fields(Type type)
        {
            if (fields.TryGetValue(type,out var found)) { return found; }
            var all=new List<FieldInfo>();
            for(var current=type; current!=null && current!=typeof(object) && current!=typeof(MonoBehaviour);current=current.BaseType)
            {
                foreach(var field in current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                {
                    var t=field.FieldType;
                    if (typeof(Delegate).IsAssignableFrom(t) || typeof(System.IO.Stream).IsAssignableFrom(t) ||
                        t==typeof(Coroutine) || typeof(YieldInstruction).IsAssignableFrom(t) || t==typeof(SocialLogSink) ||
                        t.Namespace=="Unity.Profiling" || t==typeof(Transform) || t==typeof(RectTransform) || t==typeof(GameObject) ||
                        typeof(Object).IsAssignableFrom(t) && !typeof(Trader).IsAssignableFrom(t) && !RootNames.Contains(t.Name)) { continue; }
                    if (type==typeof(PhoneCallController) && (field.Name=="raised" || field.Name=="previousLocked")) { continue; }
                    if (type==typeof(PhoneCallController) && field.Name=="nextUi" || type==typeof(PlayerAccounts) && field.Name=="next") { continue; }
                    if (type==typeof(InvestorSociety) && (field.Name=="socialWired" || field.Name=="nextSocialLogFlush")) { continue; }
                    all.Add(field);
                }
            }
            return fields[type]=all.ToArray();
        }
        // 같은 필드 이름 문자열을 객체마다 새로 만들면 저장 한 번에 수백만 개가 생긴다. 한 번만 만든다.
        private static readonly Dictionary<FieldInfo,string> fieldKeys = new();
        private static string FieldKey(FieldInfo field)
        {
            if (!fieldKeys.TryGetValue(field,out string key)) { key=field.DeclaringType.FullName+"|"+field.Name; fieldKeys.Add(field,key); }
            return key;
        }
        // 화면용 디버그 기록은 세계 상태가 아니다. 저장하지 않으며, 예전 저장에 들어 있어도 복원은 그대로 된다.
        private static bool SkipOnWrite(FieldInfo field) => field.DeclaringType==typeof(Trader) && field.Name=="orderHistory";
        public JToken WriteComponent(Component value) => Write(value,true);
        private int TypeId(Type type)
        { if (!typeIds.TryGetValue(type,out int id)) { id=Types.Count; typeIds.Add(type,id); Types.Add(type.AssemblyQualifiedName); } return id; }
        private JToken Write(object value,bool component=false,Type expected=null)
        {
            if (value==null || value is Object obj && obj==null) { return JValue.CreateNull(); }
            Type type=value.GetType();
            if (value is Object native && !component) { return new JObject { ["u"]=Key(native) }; }
            if (type==typeof(string)) { return new JValue((string)value); }
            if (type.IsPrimitive || type.IsEnum || type==typeof(decimal))
            { return expected != null && expected!=typeof(object) ? new JValue(Convert.ToString(value,Culture)) : new JObject { ["p"]=TypeId(type),["v"]=Convert.ToString(value,Culture) }; }
            if (!Allowed(type)) { throw new InvalidDataException("Unsupported session type: "+type); }
            if (!type.IsValueType && ids.TryGetValue(value,out int existing)) { return new JObject { ["r"]=existing }; }
            int id=Nodes.Count;
            if (id>=MaxNodes) { throw new InvalidDataException("Checkpoint object budget reached."); }
            var node=new JObject { ["t"]=TypeId(type) }; Nodes.Add(node);
            if (!type.IsValueType) { ids.Add(value,id); }
            if (component) { node["component"]=Key((Object)value); }
            if (value is Stock stock && !string.IsNullOrEmpty(stock.symbol)) { node["stock"]=stock.symbol; }
            if (value is IDictionary dictionary)
            {
                var entries=new JArray(); node["dict"]=entries;
                if (type.IsGenericType && type.GetGenericTypeDefinition()==typeof(SortedList<,>))
                {
                    var comparer=type.GetProperty("Comparer").GetValue(value);
                    var keys=type.GetGenericArguments()[0];
                    if (keys!=typeof(decimal)) { throw new InvalidDataException("Unsupported sorted key."); }
                    node["descending"]=((IComparer<decimal>)comparer).Compare(0m,1m)>0;
                }
                var arguments=type.GetGenericArguments();
                foreach(DictionaryEntry pair in dictionary) { entries.Add(new JArray(Write(pair.Key,false,arguments[0]),Write(pair.Value,false,arguments[1]))); }
            }
            else if (value is IEnumerable enumerable && type!=typeof(string))
            { var values=new JArray(); node["items"]=values; var itemType=type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0]; foreach(var item in enumerable) { values.Add(Write(item,false,itemType)); } }
            else
            {
                var values=new JObject(); node["fields"]=values;
                foreach(var field in Fields(type)) { if (SkipOnWrite(field)) { continue; } values[FieldKey(field)]=Write(field.GetValue(value),false,field.FieldType); }
            }
            return new JObject { ["r"]=id };
        }
        private static bool Allowed(Type type)
        {
            if (type.IsPointer || typeof(Delegate).IsAssignableFrom(type)) { return false; }
            if (type.IsPrimitive || type.IsEnum || type==typeof(string) || type==typeof(decimal) || type==typeof(System.Random)) { return true; }
            if (type.Assembly==typeof(Stock).Assembly) { return !typeof(Object).IsAssignableFrom(type) || typeof(Trader).IsAssignableFrom(type) || RootNames.Contains(type.Name); }
            if (type.IsArray) { return Allowed(type.GetElementType()); }
            if (type.IsGenericType)
            {
                var def=type.GetGenericTypeDefinition(); string name=def.FullName;
                return (def==typeof(List<>) || def==typeof(Dictionary<,>) || def==typeof(SortedList<,>) || def==typeof(HashSet<>) || def==typeof(Queue<>) ||
                    def==typeof(Stack<>) || name.StartsWith("System.ValueTuple`",StringComparison.Ordinal) || def==typeof(Nullable<>)) && type.GetGenericArguments().All(Allowed);
            }
            return type.IsValueType && type.Namespace=="UnityEngine" || type.FullName.StartsWith("System.Random+",StringComparison.Ordinal);
        }
        private static Type Resolve(string name)
        {
            var type=Type.GetType(name,false);
            if(type==null || !Allowed(type)) { throw new InvalidDataException("Unsupported checkpoint type: "+name); }
            return type;
        }
        public void Restore(JObject data)
        {
            saved=(JArray)data["nodes"]; restored=new object[saved.Count];
            Types.Merge((JArray)data["types"]);
            var roots=(JArray)data["roots"];
            // Preflight every root and field before mutating the initialized world.
            foreach(var binding in roots)
            { if (!unity.TryGetValue((string)binding["key"],out var value) || value is not Component c || !Root(c)) { throw new InvalidDataException("Session participant missing: "+binding["key"]); } }
            for(int i=0;i<saved.Count;i++)
            {
                var node=(JObject)saved[i]; Type type=Resolve((string)Types[(int)node["t"]]);
                if (node["component"]!=null) { restored[i]=unity[(string)node["component"]]; }
                else if (node["stock"]!=null)
                {
                    if(!stocks.TryGetValue((string)node["stock"],out var stock))
                    {
                        if (type!=typeof(Stock)) { throw new InvalidDataException("Invalid stock node."); }
                        stock=new Stock((string)node["stock"],(string)node["stock"],1,1) { IsListed=false };
                        stocks.Add(stock.symbol,stock);
                    }
                    restored[i]=stock;
                }
                else if(type==typeof(LeadingRoomOrders))
                { restored[i]=InvestorSociety.Instance.EnsureSessionOrders(); }
                else if(type.IsArray) { restored[i]=Array.CreateInstance(type.GetElementType(),((JArray)node["items"]).Count); }
                else if(node["descending"]!=null && (bool)node["descending"])
                { restored[i]=Activator.CreateInstance(type,Comparer<decimal>.Create((a,b)=>b.CompareTo(a))); }
                else if(node["items"]!=null || node["dict"]!=null) { restored[i]=Activator.CreateInstance(type); }
                else { restored[i]=FormatterServices.GetUninitializedObject(type); }
                if(node["fields"] is JObject values)
                {
                    var names=new HashSet<string>(Fields(type).Select(FieldKey));
                    foreach(var field in values.Properties()) { if(!names.Contains(field.Name)) { throw new InvalidDataException("Session fields changed: "+field.Name); } }
                }
            }
            // Values (including tuple keys) are populated before the hash-based collections.
            for(int i=0;i<saved.Count;i++) { FillFields(i); }
            for(int i=0;i<saved.Count;i++) { FillCollection(i); }
        }
        private readonly HashSet<int> filling = new(), filled = new();
        private void FillFields(int index)
        {
            if(filled.Contains(index) || !filling.Add(index)) { return; }
            if(saved[index]["fields"] is JObject values)
            {
                if (restored[index] is Stock stock && !values.ContainsKey("Stock|business")) { stock.business=null; }
                foreach(var field in Fields(restored[index].GetType()))
                { if(values.TryGetValue(FieldKey(field),out var token)) { field.SetValue(restored[index],Read(token,field.FieldType)); } }
            }
            filling.Remove(index); filled.Add(index);
        }
        private object Read(JToken token,Type expected)
        {
            if(token==null || token.Type==JTokenType.Null) { return null; }
            if(token.Type==JTokenType.String)
            { var target=Nullable.GetUnderlyingType(expected) ?? expected; string value=(string)token; return target.IsEnum ? Enum.Parse(target,value) : Convert.ChangeType(value,target,Culture); }
            if(token["u"]!=null)
            { if(!unity.TryGetValue((string)token["u"],out var value)) { throw new InvalidDataException("Missing native reference: "+token["u"]); } return value; }
            if(token["r"]!=null)
            { int id=(int)token["r"]; if(id<0 || id>=restored.Length) { throw new InvalidDataException("Bad session reference."); } FillFields(id); return restored[id]; }
            var type=Resolve((string)Types[(int)token["p"]]); string raw=(string)token["v"];
            return type.IsEnum ? Enum.Parse(type,raw) : Convert.ChangeType(raw,type,Culture);
        }
        private void FillCollection(int index)
        {
            var target=restored[index]; var node=saved[index];
            if(node["dict"] is JArray entries)
            { var dictionary=(IDictionary)target; var arguments=target.GetType().GetGenericArguments(); foreach(var pair in entries) { dictionary.Add(Read(pair[0],arguments[0]),Read(pair[1],arguments[1])); } }
            else if(node["items"] is JArray items)
            {
                if(target is Array array) { for(int i=0;i<items.Count;i++) { array.SetValue(Read(items[i],array.GetType().GetElementType()),i); } }
                else
                {
                    string method=target.GetType().GetGenericTypeDefinition()==typeof(Queue<>) ? "Enqueue" : target.GetType().GetGenericTypeDefinition()==typeof(Stack<>) ? "Push" : "Add";
                    var add=target.GetType().GetMethod(method);
                    var sequence=method=="Push" ? items.Reverse() : items;
                    foreach(var item in sequence) { add.Invoke(target,new[]{Read(item,target.GetType().GetGenericArguments()[0])}); }
                }
            }
        }
    }
}
