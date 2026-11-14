using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

// Headless engine boundary. Market prices, accounts, escrow, orders, fills, bot decisions and social rules are production code.
namespace UnityEngine
{
    public partial class Coroutine { internal IEnumerator iterator; internal double due; }
    public partial class MonoBehaviour
    {
        public string name="Headless"; public bool enabled=true,isActiveAndEnabled=true;
        public GameObject gameObject; public Transform transform=>gameObject?.transform;
        private readonly List<Coroutine> coroutines=new();
        public Coroutine StartCoroutine(IEnumerator iterator) { var c=new Coroutine {iterator=iterator}; coroutines.Add(c); Advance(c); return c; }
        private bool Advance(Coroutine c) { if(!c.iterator.MoveNext()) { coroutines.Remove(c); return false; } c.due=Time.time+(c.iterator.Current is WaitForSeconds wait?wait.seconds:.25); return true; }
        public void Pump() { foreach(var c in coroutines.ToArray()) { if(Time.time>=c.due) { Advance(c); } } }
        public void StopAllCoroutines() { coroutines.Clear(); }
        public void StopCoroutine(Coroutine c) { coroutines.Remove(c); }
        public static void Destroy(object obj) { }
        public T GetComponent<T>() where T:class=>this as T;
    }
    public sealed class GameObject
    {
        public string name; public bool activeSelf=true; public Transform transform=new();
        public GameObject(string name) { this.name=name; }
        public T AddComponent<T>() where T:MonoBehaviour { var value=(T)Activator.CreateInstance(typeof(T));value.gameObject=this;value.name=name; Invoke(value,"Awake");return value; }
        public void SetActive(bool active) { activeSelf=active; }
        public static void Invoke(object value,string method) { value.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)?.Invoke(value,null); }
    }
    public sealed class Transform { public void SetParent(Transform parent,bool world) { } }
    public sealed class WaitForSeconds { public float seconds; public WaitForSeconds(float s) { seconds=s; } }
    public sealed class TooltipAttribute:Attribute { public TooltipAttribute(string text) { } }
    public static partial class Time { public static float deltaTime=.25f,unscaledTime; }
    public static class Random
    {
        private static System.Random random=new(1); public static void InitState(int seed) { random=new(seed); }
        public static float value=>(float)random.NextDouble(); public static int Range(int min,int max)=>random.Next(min,max);
        public static float Range(float min,float max)=>min+(max-min)*value;
    }
    public static partial class Mathf
    {
        public static int CeilToInt(float value)=>(int)Math.Ceiling(value); public static int FloorToInt(float value)=>(int)Math.Floor(value);
        public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
        public static float Exp(float n)=>(float)Math.Exp(n);public static float Sqrt(float n)=>(float)Math.Sqrt(n);
    }
    public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a=1) { this.r=r;this.g=g;this.b=b;this.a=a; } public static Color gray=>new(.5f,.5f,.5f); public static Color red=>new(1,0,0);public static Color green=>new(0,1,0);public static Color yellow=>new(1,1,0); }
    public static class ColorUtility { public static string ToHtmlStringRGB(Color color)=>"888888"; }
    public static class Debug { public static void Log(object text) { } public static void LogWarning(object text) { } public static void LogError(object text) { Console.WriteLine(text); } }
}
public sealed class StockManager
{
    public static StockManager Instance; public static event Action<Stock> OnStockListed,OnStockDelisted;
    public bool SetupConfirmed=true;public EconomySettings economy=new();public readonly List<Stock> stocks=new();public Stock CurrentStock=>stocks[0];
    public void AssignInitialCash(Trader trader) { }
}
public sealed class MarketDayController
{
    public static MarketDayController Instance; public static bool CanTrade=>Instance!=null && Instance.Ready;
    public static int Session=>Instance?.Day??0;
    public bool Ready=true;public int Day=1,GameMinute=540,maxBots=2000,seed=73129;public readonly List<TradeBot> Bots=new();
    public MarketConversationFocus ConversationFocus=new();
    public bool Register(TradeBot bot) { bot.Social=new BotSocialState(Bots.Count+1,seed,bot.GetType().Name);Bots.Add(bot);return true; }
    public void Unregister(TradeBot bot) { Bots.Remove(bot); }
    public void Consider(TradeBot bot,Stock stock) { }
    public void UpdatePositionIndex(TradeBot bot,Stock stock) { if(!bot.Social.Positions.ContainsKey(stock)) { bot.Social.Positions[stock]=new(); } }
    public void Sold(Trader trader,Stock stock,decimal quantity,decimal price,decimal basis)
    { if(trader is TradeBot bot && quantity>0) { UpdatePositionIndex(bot,stock);var memory=bot.Social.Positions[stock];memory.SellPrice=price;memory.SellTime=UnityEngine.Time.time;memory.SellProfit=(price-basis)*quantity; } }
}
public sealed partial class InvestorSociety { public void Forget(TradeBot bot) { } }
public class ExchangeBot:RandomBot { public static ExchangeBot Instance;public OrderPurpose SubmittedPurpose;public int SubmittedSlot;public void OnTradeSettled(Stock stock) { } }
public class WhaleBot:RandomBot { public bool IsDisposalSubmission; }
public class ScalperBot:RandomBot { }
public class SmartBot:RandomBot { }
public class HolderBot:RandomBot { }
public class PanicBot:RandomBot { }
public class InstitutionBot:RandomBot { }
public class InternalValueBot:RandomBot { }
public class FomoBot:RandomBot { }
public class StupidBot:RandomBot { }
public class WhaleFollowerBot:RandomBot { }
public class FakeValueBot:RandomBot { }
public class StockNewsAttentionManager:UnityEngine.MonoBehaviour
{ public static StockNewsAttentionManager Instance; public void RegisterStock(Stock stock) { } public float GetAttention(Stock stock)=>1; }
public sealed class WorldDramaDirector { public static WorldDramaDirector Instance; public bool CanFundBuy(TradeBot bot)=>true;public bool TryFundDecision(TradeBot bot,Stock stock)=>false; }
public sealed class MarketRhythmDirector
{ public static MarketRhythmDirector Instance;public void BoundOrder(TradeBot bot,Stock stock,bool buy,ref decimal price,ref decimal quantity) { }public bool TryAct(TradeBot bot,Stock stock,bool promised=false)=>false; }
public static class PsychologicalLevelManager
{ public static event Action<Stock,decimal> OnWallForming,OnBreakthroughImminent; }
public static class CandleHistoryManager
{ public static int StableSeed(int seed,string value) { unchecked { foreach(char c in value) { seed=seed*31+c; } return seed; } } }
