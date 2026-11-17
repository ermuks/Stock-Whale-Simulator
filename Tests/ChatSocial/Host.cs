using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

// Only Unity lifetime/time/serialization and external account inputs are adapted here.
// Legacy chat methods are extracted verbatim at build time; all social methods are production sources.
namespace UnityEngine
{
    public class MonoBehaviour { }
    public class Coroutine { }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int n) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string text) { } }
    public class MinAttribute : Attribute { public MinAttribute(float n) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class SerializeField : Attribute { }
    public static class Time { public static float time, timeScale = 1; }
    public static class Mathf
    {
        public static float Min(float a,float b)=>Math.Min(a,b);
        public static int Min(int a,int b)=>Math.Min(a,b);
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Clamp(float a,float b,float c)=>Math.Clamp(a,b,c);
        public static int Clamp(int a,int b,int c)=>Math.Clamp(a,b,c);
        public static float Clamp01(float a)=>Math.Clamp(a,0,1);
        public static float Abs(float a)=>Math.Abs(a);
        public static float Pow(float a,float b)=>(float)Math.Pow(a,b);
        public static float MoveTowards(float a,float b,float d)=>a<b?Math.Min(a+d,b):Math.Max(a-d,b);
    }
    public static class PlayerPrefs { public static string GetString(string key,string fallback)=>fallback; }
    public class TextAsset { public string text; }
    public static class Resources
    {
        public static string Root;
        public static T Load<T>(string key) where T:class => new TextAsset { text = File.ReadAllText(Path.Combine(Root,"Assets/Resources",key+".json")) } as T;
    }
    public static class JsonUtility
    {
        public static readonly JsonSerializerOptions Options = MakeOptions();
        private static JsonSerializerOptions MakeOptions()
        {
            var resolver=new DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(info=>
            {
                var fields=info.Type.GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Where(f=>f.GetCustomAttribute<SerializeField>()!=null).ToArray();
                if(fields.Length==0) { return; }
                info.CreateObject=()=>System.Runtime.Serialization.FormatterServices.GetUninitializedObject(info.Type);
                foreach(var field in fields)
                {
                    var property=info.CreateJsonPropertyInfo(field.FieldType,field.Name);
                    property.Get=field.GetValue;property.Set=field.SetValue;info.Properties.Add(property);
                }
            });
            return new JsonSerializerOptions { IncludeFields=true, IgnoreReadOnlyProperties=true,TypeInfoResolver=resolver };
        }
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json,Options);
        public static string ToJson(object item) => item == null ? "null" : JsonSerializer.Serialize(item,item.GetType(),Options);
        public static string ToJson(object item, bool prettyPrint) => ToJson(item);
    }
}
namespace Unity.Profiling { public struct ProfilerMarker { public ProfilerMarker(string key) { } } }
public enum StockRiskGrade { Safe, Warning, Danger, HighRisk, Halted }
public sealed class Stock
{
    public StockRiskGrade RiskGrade;
    public int RiskVersion;
    public bool RiskPending;
    public string stockName = "노보손해보험", symbol = "NM";
    public decimal currentPrice = 1000m;
    public bool IsListed = true, IsDelisted, IsTradingHalted;
    public bool AcceptsOrders => !IsDelisted && !IsTradingHalted;
    public event Action<Stock,decimal,decimal,IOrderOwner,IOrderOwner,Order> OnMatchedTrade;
    public void Matched(decimal price,decimal quantity,IOrderOwner buyer,IOrderOwner seller,Order resting) { OnMatchedTrade?.Invoke(this,price,quantity,buyer,seller,resting); }
    public readonly TestBook orderBook = new();
}
public sealed class TestBook
{
    public readonly List<Order> Orders=new();
    public void CopyOwnerOrders(Trader owner,List<Order> target) { target.Clear();target.AddRange(Orders.Where(o=>o.owner==owner)); }
    public void CancelOrder(Order order,Stock stock) { Orders.Remove(order); }
    public bool IsActive(Order order)=>Orders.Contains(order);
    public void GetTopBids(List<(decimal price,decimal volume)> target,int n) { target.Clear(); target.Add((999m,100m)); }
    public void GetTopAsks(List<(decimal price,decimal volume)> target,int n) { target.Clear(); target.Add((1001m,100m)); }
}
public interface IOrderOwner { }
public static class GameLocale { public static bool Ready=>true; public static string Code="ko"; }
// Same FNV-1a hash as the game's CompanyBusiness.Hash, so deterministic picks match production.
public sealed class CompanyBusiness { public static uint Hash(string text) { uint value = 2166136261; foreach (char c in text) { value = unchecked((value ^ c) * 16777619); } return value; } }
public sealed class Order { public Trader owner;public float createdAt;public decimal price,volume,costBasis;public bool buy; }
public class Trader : IOrderOwner { }
public static class PriceTickPolicy { public static decimal RoundToTick(decimal price)=>decimal.Round(price,0); }
public sealed class TradeBot : Trader
{
    public float riskPreference = .5f;
    public bool CanMakeSocialPromise => true;
    public bool isActiveAndEnabled = true;
    public bool IsExchangeParticipant;
    public BotSocialState Social;
    public int DailySettledDay;
    public decimal DailyReturn;
    public readonly Dictionary<Stock,(decimal quantity,decimal basis)> Positions = new();
    public decimal cash=100000m,ReservedCash;
    public decimal Equity()=>cash+Positions.Sum(p=>p.Value.quantity*p.Key.currentPrice);
    public decimal GetHolding(Stock stock)=>GetOwnedVolume(stock);
    public void BuyLimit(decimal price,decimal volume,int subTickSteps,Stock stock) { stock.orderBook.Orders.Add(new Order {owner=this,price=price,volume=volume,buy=true,createdAt=UnityEngine.Time.time}); }
    public void SellLimit(decimal price,decimal volume,int subTickSteps,Stock stock) { stock.orderBook.Orders.Add(new Order {owner=this,price=price,volume=volume,buy=false,createdAt=UnityEngine.Time.time}); }
    public decimal GetOwnedVolume(Stock stock) => Positions.TryGetValue(stock,out var item)?item.quantity:0m;
    public decimal GetOwnedAverage(Stock stock) => Positions.TryGetValue(stock,out var item)?item.basis:0m;
    public decimal GetOwnedCost(Stock stock) => Positions.TryGetValue(stock,out var item)?item.basis*item.quantity:0m;
    public bool KnowsRealValue => IsExchangeParticipant;
    public void SuspendSession() { }
}
public sealed class StockManager
{
    public static event Action<Stock> OnStockListed,OnStockDelisted;
    public static StockManager Instance;
    public bool SetupConfirmed = true;
    public readonly List<Stock> stocks = new();
}
public sealed partial class InvestorSociety
{
    public const int MaximumRoomId = 4;
    public const int PlayerRoomCount = 3;
    public const int AvailablePlayerRoomCount = 1;
    public static InvestorSociety Instance;
    public sealed class Room { public readonly SocialRoomState Social = new(); public readonly HashSet<int> Members = new(); public int Id,OwnerBotId; public bool PlayerOwned=true; public string Title; public string DisplayName => PlayerOwned ? "내 리딩방" : Title; public LeadingRoomOrders.Command Command; public long Version; public float NextConversation; }
    public readonly Room[] Rooms = { new() {Id=1},new() {Id=2},new() {Id=3},new() {Id=4,PlayerOwned=false} };
    private MarketDayController day;
    private readonly Random random = new(73129);
    private readonly Dictionary<int,int> suggestions = new();
    private float nextContact;
    public int Joins;
    public string LastAction;
    public void PostRoomNotice(int room,ChatRoomNotice notice,string text) { }
    public void Speak(TradeBot bot,int room,string text,InvestorMessageContext context,ChatEvent kind=ChatEvent.Agreement) { }
    public Room GetRoom(int id) => id>0 && id<=4 ? Rooms[id-1] : null;
    public void OfferExistingSpeech(TradeBot bot,int room,InvestorMessageContext context,ChatEvent kind) { Offer(bot,room,SocialMotive.WITNESSED_EVENT); }
    public void ReadBeforeSpeech(TradeBot bot) { }
    public InvestorSocietySettings settings=new();
    public long NextEventId()=>ChatSocialIds.Next();
    public void Trace(TradeBot bot,string text) { }
    public void RecordOutcome(TradeBot bot,InvestorExperience experience) { bot.Social.Psychology.Record(experience,settings); }
}
public sealed class FakeNewsManager
{
    public static FakeNewsManager Instance;
    public double PlayerCooldown;
    public bool RestorePlayerCooldown(double remaining,long savedUtcTicks) { PlayerCooldown=remaining; return true; }
}
public sealed partial class MarketDayController
{
    public TradeBot FindBot(int id) => Bots.FirstOrDefault(bot=>bot.Social?.Id==id);
    private void StopCoroutine(UnityEngine.Coroutine work) { }
    public BotChatTemplateCatalog Catalog { get; private set; }
    public readonly List<BotChatMessage> AllMessages = new();
    public void InitializeReplay(int chosenSeed)
    {
        Instance = this; seed = chosenSeed; random = new Random(seed); messages = new BotChatMessage[256];
        Catalog = new BotChatTemplateCatalog(UnityEngine.Resources.Load<UnityEngine.TextAsset>("Chat/BotChatTemplates.ko").text,Console.WriteLine);
        Phase = MarketPhase.Trading; Ready = true;
        for (int id = 1; id <= 20; id++) { Bots.Add(new TradeBot { Social = new BotSocialState(id,seed,"RandomBot") }); }
    }
    public void ReplayStep(float seconds, double gameMinute)
    {
        UnityEngine.Time.time = seconds; Day = (int)(gameMinute/1440)+1;
        elapsed = (float)((gameMinute%1440-540)/390*tradingSeconds);
        long before = sequence; ProcessChat();
        if (before != sequence) { AllMessages.Add(GetMessage(MessageCount-1)); }
    }
}

// Unity account/clock adapter is absent in the conversation-only harness.
// Paid joins, money movement and UI are exercised by Validation/DailyIncome's actual-scene driver.
public sealed class DailyIncomeController
{
    [Serializable] public sealed class SaveData
    {
        public string worldId, company, realizedAtOpen;
        public DailyIncomeLedger.Snapshot ledger;
    }
    public static DailyIncomeController Instance => null;
    public SaveData Capture() => null;
    public bool CanRestore(SaveData saved,string worldId) => false;
    public bool Restore(SaveData saved,string worldId) => false;
    public bool Enter(TradeBot bot,int room,out string reason) { reason = ""; return true; }
}
