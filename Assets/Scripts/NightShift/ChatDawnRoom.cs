using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 새벽 관찰실(NpcLeadingRoom.RoomId)의 대화.
//
// 이 방에는 급등락 군중·차트 수다·잔움직임·토론 같은 라운지의 장치가 돌지 않는다. 말은 전부 대화 틀(ChatSocialPipeline)에서 나온다.
// 그런데 그 틀의 대사는 어느 방에서나 쓰는 두루뭉술한 말이었다: "가격만으론 판단 어렵다", "그렇게 보는 이유는 알겠어요",
// "그럴 가능성도 있겠네", "차트랑 눈싸움 중 ㅋㅋ". 무엇을 보고 하는 말인지, 그래서 어쩌겠다는 것인지가 없었다. 물음("얼마 보고 있음?")에는
// "한 번 움직인 걸로는 모르지. 다음 흐름 보자"가 답으로 달렸고, 맞장구에 맞장구가 달렸다. 영어·일본어는 그 말의 번역이었다.
//
// 여기서는 두 가지를 바꾼다.
//   1) 무엇을 말할 수 있는가(AdjustDawnWeight): 근거 없는 혼잣말·잡담·놀림·욕은 거의 막는다. 혼자 꺼내는 말은 지금 볼 것이 있을 때만 한다
//      (그 종목의 상황이 조용하지 않거나, 자기가 들고 있거나, 방금 사고판 종목). 같은 종목에 같은 종류의 말이 방금 나왔으면 또 하지 않는다.
//      받지 않은 물음이 떠 있으면 새 물음을 얹지 않는다. 답글에서는 되묻기와 근거 있는 반박을 늘리고 빈 맞장구를 줄인다.
//      반박과 맞장구는 그 봇이 그 종목을 실제로 어떻게 보는지(BelievedPrice)에 따른다: 생각이 같은 사람에게 반박하지 않고, 다른 사람에게 맞장구치지 않는다.
//      반박을 받은 사람은 가끔 "내가 잘못 봤네" 하고 물러선다. 말과 말 사이에는 틈을 둔다(DawnIdleGapSec).
//   2) 어떻게 말하는가(TryDawnLine): 그 언어로 따로 쓴 대사(Chat/chat_dawn.ko / .en / .ja)에서 고른다. 대사에는 지금의 실제 값이 들어간다:
//      현재가, 당일 고가·저가와 그 가운데 값, 전일 대비, 고점·저점 대비, 호가창의 큰 매수·매도 잔량이 선 값, 자기 평단과 수익률.
//      값이 든 문장을 주로 고르고(GroundedChance), 채울 값이 없는 문장은 고르지 않는다.
// 자랑·한탄(인증 카드가 붙는 말), 운영자의 주문 공개, 공지에 딸린 말은 예전 그대로다.

/// <summary>상황을 새벽 관찰실의 얘깃거리로 묶은 것.</summary>
public enum ChatDawnBucket { Flat, Up, Dip, Down, Break, Bounce }

/// <summary>새벽 관찰실의 대사 고르기. 게임 상태를 건드리지 않는 계산만 있다(에디터 점검 도구가 같은 계산을 쓴다).</summary>
public static class ChatDawnTalk
{
    /// <summary>값이 든 문장과 없는 문장이 다 있을 때 값이 든 쪽을 고를 확률. 이 방은 값을 대고 말한다.</summary>
    public const double GroundedChance = .75;
    /// <summary>실제 값이 들어가는 빈칸. 이런 빈칸이 하나라도 든 문장이 "근거 있는 문장"이다.</summary>
    public static readonly string[] LevelSlots = { "{price}", "{dayHigh}", "{dayLow}", "{mid}", "{support}", "{resist}", "{dayPct}", "{fromHigh}", "{fromLow}", "{avg}", "{myPct}", "{bidWall}", "{askWall}" };
    /// <summary>
    /// 받칠 자리·막힐 자리로 부를 수 있는 거리(현재가 대비): 1%보다 가깝면 이미 그 자리에 있는 것이고, 12%보다 멀면 지금의 계획에 넣을 값이 아니다.
    /// 계획을 말하는 문장(어디서 받는다, 어디 깨지면 자른다)에 당일 고가·저가를 넣을 때도 PlanReach(10%)보다 먼 값은 넣지 않는다.
    /// </summary>
    public const decimal LevelNear = .01m, LevelFar = .12m, PlanReach = 10m;
    private static readonly List<int> plain = new(), grounded = new();

    public static ChatDawnBucket Bucket(ChatSituation kind) => kind switch
    {
        ChatSituation.DriftUp or ChatSituation.Breakout or ChatSituation.Surge or ChatSituation.MonsterRally or ChatSituation.MonsterDip => ChatDawnBucket.Up,
        ChatSituation.Pullback => ChatDawnBucket.Dip,
        ChatSituation.DriftDown or ChatSituation.SlowBleed or ChatSituation.RoundTrip => ChatDawnBucket.Down,
        ChatSituation.Crash or ChatSituation.Capitulation => ChatDawnBucket.Break,
        ChatSituation.DeadCatBounce or ChatSituation.Recovery => ChatDawnBucket.Bounce,
        _ => ChatDawnBucket.Flat
    };
    /// <summary>오르는 날인가, 내리는 날인가, 아무 일 없는 날인가("up"·"down"·"flat").</summary>
    public static string Direction(ChatDawnBucket bucket) => bucket == ChatDawnBucket.Up || bucket == ChatDawnBucket.Bounce ? "up" : bucket == ChatDawnBucket.Flat ? "flat" : "down";
    /// <summary>그 종목을 어떻게 보는가: 지금 값이 자기가 믿는 값보다 3% 넘게 싸면 "bull", 3% 넘게 비싸면 "bear", 아니면 "neutral".</summary>
    public static string View(decimal believed, decimal price) => price <= 0m || believed <= 0m ? "neutral" : believed > price * 1.03m ? "bull" : believed < price * .97m ? "bear" : "neutral";
    private static bool Attack(SocialAct act) => act == SocialAct.REBUT || act == SocialAct.DOUBT || act == SocialAct.MOCK;

    /// <summary>
    /// 이 말이 어느 대사 묶음에서 나오는가. 이 방의 대사로 바꾸지 않는 말(자랑·한탄·방장에게 하는 말 …)이면 null.
    ///   read.{상황}.{처지}   혼자 꺼내는 관찰            fill.buy / fill.sell   방금 한 자기 매매
    ///   doubt.{up·down·flat} 지금 흐름을 못 믿겠다는 말   ask.{up·down·flat}    방에 던지는 물음
    ///   answer.{견해}.{held·watch} 물음에 대한 답(어쩔 것인지, 어느 값에서)
    ///   follow               답이나 관찰에 다는 되물음     rebut.{bull·bear}     근거를 댄 반박(상대와 반대쪽)
    ///   agree.{견해}         까닭을 보탠 맞장구            concede               잘못 봤다고 물러서는 말
    ///   comfort              물린 사람에게 하는 말
    /// </summary>
    public static string Group(SocialAct act, SocialAct? parentAct, bool selfEvent, ChatDawnBucket bucket, ChatStance stance, string view, string parentView, bool bought, bool sold, bool held)
    {
        string direction = Direction(bucket); bool reply = parentAct.HasValue;
        string read = "read." + bucket + "." + ChatSituationMath.StanceKey(stance);
        switch (act)
        {
            case SocialAct.REACT:
                if (!reply && selfEvent && sold && !held) { return "fill.sell"; }
                if (!reply && selfEvent && bought && held) { return "fill.buy"; }
                return read;
            case SocialAct.DOUBT:
                if (reply) { return "rebut." + Against(parentView, view, direction); }
                // 오르는 날 좋게 보는 사람, 내리는 날 나쁘게 보는 사람은 의심할 것이 없다. 본 것을 말한다.
                if (direction == "up" && view == "bull" || direction == "down" && view == "bear") { return read; }
                return "doubt." + direction;
            case SocialAct.REBUT: case SocialAct.MOCK:
                return reply ? "rebut." + Against(parentView, view, direction) : null;
            case SocialAct.ASK: return reply ? "follow" : "ask." + direction;
            case SocialAct.ANSWER: return reply ? "answer." + view + "." + (held ? "held" : "watch") : null;
            case SocialAct.AGREE: return !reply ? null : Attack(parentAct.Value) ? "concede" : "agree." + view;
            case SocialAct.APOLOGIZE: return reply ? "concede" : null;
            case SocialAct.COMFORT: return reply ? "comfort" : null;
            default: return null;
        }
    }
    /// <summary>반박은 상대와 반대쪽에서 한다. 상대의 생각을 모르면 자기 생각대로, 그것도 없으면 오늘 흐름의 반대쪽에서.</summary>
    private static string Against(string parentView, string view, string direction)
        => parentView == "bull" ? "bear" : parentView == "bear" ? "bull" : view != "neutral" ? view : direction == "up" ? "bear" : "bull";

    /// <summary>
    /// 상황에서 읽은 값을 빈칸에 채운다. 말이 안 되는 값은 넣지 않는다(고점에 붙어 있는데 "고점 대비"를 말하지 않는다. 고가와 저가가 붙어 있으면 둘 다 말하지 않는다).
    ///   {dayPct} 전일 대비("+3.2%") · {dayHigh} {dayLow} 당일 고가·저가 · {mid} 그 가운데 값(호가 단위로) · {fromHigh} 고점 대비("-4%") · {fromLow} 저점 대비("+6%")
    ///   {support} {resist} 지금 값 아래·위의 가장 가까운 자리(1%~12% 거리 안에 있을 때만)
    /// </summary>
    public static void FillRead(in ChatSituationRead read, IDictionary<string, string> slots)
    {
        if (!read.valid) { return; }
        slots["dayPct"] = ChatSituationMath.SignedPercent(read.dayPct);
        if (read.high > read.low * 1.01m)
        {
            slots["dayHigh"] = InvestorSociety.Money(read.high); slots["dayLow"] = InvestorSociety.Money(read.low);
            decimal mid = PriceTickPolicy.RoundToTick((read.high + read.low) / 2m), price = read.price;
            slots["mid"] = InvestorSociety.Money(mid);
            // {support} 지금 값 아래에서 받칠 만한 가장 가까운 자리(가운데 값 또는 저가), {resist} 위에서 막힐 만한 가장 가까운 자리(가운데 값 또는 고가).
            // "깨지면 자른다"·"넘으면 따라간다" 같은 문장은 이 둘을 쓴다. 가운데 값이 지금 값의 위인지 아래인지는 날마다 다르기 때문이다.
            decimal support = mid <= price * (1m - LevelNear) ? mid : read.low, resist = mid >= price * (1m + LevelNear) ? mid : read.high;
            if (support <= price * (1m - LevelNear) && support >= price * (1m - LevelFar)) { slots["support"] = InvestorSociety.Money(support); }
            if (resist >= price * (1m + LevelNear) && resist <= price * (1m + LevelFar)) { slots["resist"] = InvestorSociety.Money(resist); }
        }
        if (read.fromHigh <= -1m) { slots["fromHigh"] = ChatSituationMath.SignedPercent(read.fromHigh); }
        if (read.fromLow >= 1m) { slots["fromLow"] = ChatSituationMath.SignedPercent(read.fromLow); }
    }
    /// <summary>
    /// 관찰(read.…)이 아닌 말은 계획을 말한다("{dayLow}돈 깨지면 자름", "{dayHigh}돈 넘으면 더 삼"). 그런 문장에 한참 먼 고가·저가가 들어가면 빈말이 된다
    /// (−30%인 날의 "고가 돌파하면 따라감"). 지금 값에서 10% 넘게 떨어진 고가·저가는 빼서, 그 값이 든 문장을 고르지 않게 한다.
    /// </summary>
    public static void NarrowForPlan(string group, in ChatSituationRead read, IDictionary<string, string> slots)
    {
        if (!read.valid || group == null || group.StartsWith("read.", StringComparison.Ordinal)) { return; }
        if (read.fromHigh < -PlanReach) { slots.Remove("dayHigh"); }
        if (read.fromLow > PlanReach) { slots.Remove("dayLow"); }
    }
    public static bool Grounded(string line)
    {
        foreach (string slot in LevelSlots) { if (line.IndexOf(slot, StringComparison.Ordinal) >= 0) { return true; } }
        return false;
    }

    /// <summary>
    /// 묶음에서 한 줄을 고른다(빈칸은 아직 안 채운 틀). 채울 수 없는 문장과 방금 나온 문장(recent: 묶음 안의 번호로 묻는다)은 피한다. 없으면 null.
    /// 메인 스레드에서만 부른다.
    /// </summary>
    public static string Pick(string code, string group, IReadOnlyDictionary<string, string> slots, Func<double> random, Func<int, bool> recent, out int index)
    {
        index = -1;
        string[] pool = ChatDawnAssets.Lines(code, group);
        if (pool.Length == 0) { return null; }
        for (int pass = 0; pass < 2; pass++)
        {
            plain.Clear(); grounded.Clear();
            for (int i = 0; i < pool.Length; i++)
            {
                if (!ChatReactionEngine.Fillable(pool[i], slots) || pass == 0 && recent != null && recent(i)) { continue; }
                (Grounded(pool[i]) ? grounded : plain).Add(i);
            }
            if (plain.Count + grounded.Count > 0) { break; }
        }
        if (plain.Count + grounded.Count == 0) { return null; }
        var from = grounded.Count > 0 && (plain.Count == 0 || random() < GroundedChance) ? grounded : plain;
        index = from[(int)(random() * from.Count) % from.Count];
        return pool[index];
    }
}

[Serializable] public sealed class ChatDawnLocale { public int schemaVersion = 1; public ChatMarketGroup[] groups; }

/// <summary>언어별 새벽 관찰실 대사(Chat/chat_dawn.ko / .en / .ja). 읽지 못한 언어는 예전 대사(대화 틀)로 말한다.</summary>
public static class ChatDawnAssets
{
    private static Dictionary<string, Dictionary<string, string[]>> locales;
    private static readonly string[] None = Array.Empty<string>();
    /// <summary>언어 코드로 파일 내용을 돌려주는 함수. 비워 두면 Resources에서 읽는다(에디터 점검 도구는 파일에서 바로 읽는다).</summary>
    public static Func<string, string> Reader;
    public static readonly List<string> Problems = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { locales = null; Reader = null; Problems.Clear(); }

    /// <summary>있어야 하는 묶음의 이름 전부. 하나라도 빠지면 그 언어는 읽지 않는다(빠진 묶음에서만 예전 대사가 섞여 나오면 더 어색하다).</summary>
    public static List<string> RequiredGroups()
    {
        var keys = new List<string> { "fill.buy", "fill.sell", "follow", "concede", "comfort", "rebut.bull", "rebut.bear" };
        foreach (ChatDawnBucket bucket in Enum.GetValues(typeof(ChatDawnBucket))) { foreach (string stance in new[] { "gain", "loss", "watch" }) { keys.Add("read." + bucket + "." + stance); } }
        foreach (string direction in new[] { "up", "down", "flat" }) { keys.Add("doubt." + direction); keys.Add("ask." + direction); }
        foreach (string view in new[] { "bull", "bear", "neutral" }) { keys.Add("agree." + view); keys.Add("answer." + view + ".held"); keys.Add("answer." + view + ".watch"); }
        return keys;
    }
    public static void Reload()
    {
        var loaded = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.Ordinal); Problems.Clear();
        foreach (string code in ChatHypeAssets.Languages)
        {
            try
            {
                string json = Reader != null ? Reader(code) : Resources.Load<TextAsset>("Chat/chat_dawn." + code)?.text;
                if (string.IsNullOrEmpty(json)) { Problems.Add(code + ": missing chat_dawn." + code); continue; }
                var data = JsonUtility.FromJson<ChatDawnLocale>(json);
                if (data == null || data.schemaVersion != 1 || data.groups == null || data.groups.Length == 0) { throw new ArgumentException("Invalid chat_dawn schema"); }
                var index = new Dictionary<string, string[]>(StringComparer.Ordinal);
                foreach (var group in data.groups)
                {
                    if (group == null || string.IsNullOrEmpty(group.key) || group.texts == null || group.texts.Length == 0 || index.ContainsKey(group.key)) { throw new ArgumentException("Invalid/duplicate chat_dawn group: " + group?.key); }
                    foreach (string text in group.texts) { if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("Empty chat_dawn text: " + group.key); } }
                    index[group.key] = group.texts;
                }
                foreach (string key in RequiredGroups()) { if (!index.ContainsKey(key)) { throw new ArgumentException("Missing chat_dawn lines: " + key); } }
                loaded[code] = index;
            }
            catch (ArgumentException ex) { Problems.Add(code + ": " + ex.Message); Debug.LogWarning("새벽 관찰실 대사를 읽지 못했습니다(" + code + "). 이 언어는 예전 대사로 말합니다: " + ex.Message); }
        }
        locales = loaded;
    }
    public static bool Has(string code) { if (locales == null) { Reload(); } return locales.ContainsKey(code ?? ""); }
    public static string[] Lines(string code, string key)
    {
        if (locales == null) { Reload(); }
        return locales.TryGetValue(code ?? "", out var index) && index.TryGetValue(key, out var texts) ? texts : None;
    }
    public static IEnumerable<string> Keys(string code)
    {
        if (locales == null) { Reload(); }
        return locales.TryGetValue(code ?? "", out var index) ? index.Keys : (IEnumerable<string>)None;
    }
}

public sealed partial class InvestorSociety
{
    /// <summary>새벽 관찰실에서 혼자 꺼내는 말 사이의 틈(초): 한가해서 꺼내는 말, 자기 매매·시세가 계기인 말.</summary>
    private const double DawnIdleGapSec = 7, DawnEventGapSec = 3;
    /// <summary>같은 종목에 같은 종류의 말이 이 시간(초) 안에 나왔으면 또 하지 않는다. 받지 않은 물음은 이 시간 동안 기다려 준다.</summary>
    private const double DawnSameIdeaSec = 40, DawnOpenQuestionSec = 25;

    private string DawnViewOf(TradeBot bot, string symbol)
    {
        var stock = FindSocialStock(symbol);
        return bot == null || stock == null ? "neutral" : ChatDawnTalk.View(bot.BelievedPrice(stock), stock.currentPrice);
    }

    /// <summary>
    /// 새벽 관찰실에서 이 말을 해도 되는가, 얼마나 하고 싶은가. 다른 방의 말은 건드리지 않는다.
    /// 운영자의 주문 공개(signal)와 공지에 딸린 말(강퇴 경고, 위험 공지, 기사)은 그대로 둔다.
    /// </summary>
    private void AdjustDawnWeight(TradeBot bot, SocialIntent intent, SocialFacts facts, SocialAct act, ref double weight)
    {
        if (intent.roomId != NpcLeadingRoom.RoomId || weight <= 0 || facts == null || facts.signal || facts.directLead) { return; }
        bool watch = facts.notice == "WATCH_RISE" || facts.notice == "WATCH_FALL";
        if (!string.IsNullOrEmpty(facts.notice) && !watch)
        {
            // "나만 없어" 같은 조바심은 이 방에서는 드물게만 나온다.
            if (facts.notice == "FOMO") { weight *= .25; }
            return;
        }
        bool reply = intent.replyToId != 0 || intent.threadId != 0;
        if (act == SocialAct.INSULT) { weight = 0; return; }
        if (act == SocialAct.MOCK) { weight *= reply ? .15 : 0; return; }
        if (act == SocialAct.SMALLTALK) { weight *= .08; return; }
        var room = SocialRoom(intent.roomId); double now = SocialTime.Seconds;
        if (!reply)
        {
            // 말과 말 사이에 틈을 둔다. 답글은 기다리지 않는다.
            double last = double.NegativeInfinity;
            foreach (var said in room.recent) { if (said.realAt > last) { last = said.realAt; } }
            if (now - last < (intent.motive == SocialMotive.IDLE ? DawnIdleGapSec : DawnEventGapSec)) { weight = 0; return; }
            if (act == SocialAct.REACT || act == SocialAct.DOUBT)
            {
                // 볼 것이 있어야 말한다: 그 종목이 조용하지 않거나, 들고 있거나, 방금 사고팔았다.
                bool grounded = !string.IsNullOrEmpty(facts.situation) || facts.held || facts.bought || facts.sold;
                if (!grounded || string.IsNullOrEmpty(facts.symbol)) { weight = 0; return; }
                // 자기 매매 직후에 스스로를 의심하는 말은 하지 않는다.
                if (act == SocialAct.DOUBT && intent.motive == SocialMotive.SELF_EVENT) { weight = 0; return; }
                // 같은 종목에 같은 종류의 말이 방금 나왔으면 또 하지 않는다(같은 얘기를 말만 바꿔 되풀이하지 않는다).
                foreach (var said in room.recent)
                {
                    if (said.replyToId == 0 && said.act == act && said.facts != null && said.facts.symbol == facts.symbol && now - said.realAt < DawnSameIdeaSec) { weight = 0; return; }
                }
            }
            else if (act == SocialAct.ASK)
            {
                if (string.IsNullOrEmpty(facts.symbol)) { weight = 0; return; }
                // 아직 아무도 받지 않은 물음이 떠 있으면 새 물음을 얹지 않는다.
                foreach (var said in room.recent)
                {
                    if (said.act == SocialAct.ASK && said.replyToId == 0 && said.threadId == 0 && now - said.realAt < DawnOpenQuestionSec) { weight = 0; return; }
                }
                weight *= 1.3;
            }
            else if (act == SocialAct.BRAG) { weight *= .6; }
            return;
        }
        if (intent.replyToId == 0 || !socialMessageIndex.TryGetValue(intent.replyToId, out var parent)) { return; }
        bool attacked = parent.act == SocialAct.REBUT || parent.act == SocialAct.DOUBT || parent.act == SocialAct.MOCK;
        string mine = DawnViewOf(bot, facts.symbol);
        string theirs = SocialBots.TryGetValue(parent.senderId, out var author) ? DawnViewOf(author, parent.facts?.symbol ?? facts.symbol) : "neutral";
        bool opposed = mine != "neutral" && theirs != "neutral" && mine != theirs;
        switch (act)
        {
            case SocialAct.AGREE:
                // 반박을 받은 사람은 가끔 물러선다. 그 밖의 맞장구는 줄이고, 생각이 다른 사람에게는 맞장구치지 않는다.
                if (attacked) { weight *= 2; } else if (opposed) { weight = 0; } else { weight *= .5; }
                break;
            case SocialAct.APOLOGIZE: weight *= 1.5; break;
            // 되묻기: "그럼 손절은 어디?" 답이 달리면서 얘기가 깊어진다.
            case SocialAct.ASK: weight *= 2; break;
            case SocialAct.REBUT:
                // 반박은 생각이 다를 때만 한다. 같은 쪽을 보는 사람끼리 말꼬리를 잡지 않는다.
                if (mine == theirs) { weight = 0; } else { weight *= 1.3; }
                break;
        }
    }

    /// <summary>새벽 관찰실에서는 답글에 되묻는 말을 늘 할 수 있다(되묻는 대사 묶음 follow가 있다).</summary>
    private static bool DawnFollowUp(SocialIntent intent) => intent.roomId == NpcLeadingRoom.RoomId && ChatDawnAssets.Has(GameLocale.Code);

    /// <summary>
    /// 새벽 관찰실의 대화는 한두 마디에서 끊기지 않게 한다: 물음은 답과 되물음까지(5번), 그냥 얘기도 4번은 오간다.
    /// 다툼은 길게 끌지 않는다(6번까지).
    /// </summary>
    private void DawnThreadTurns(SocialThread thread)
    {
        if (thread == null || thread.roomId != NpcLeadingRoom.RoomId) { return; }
        thread.maxTurns = thread.kind == SocialThreadKind.ARGUMENT ? Math.Min(thread.maxTurns, Math.Max(thread.turnCount, 6)) : Math.Max(thread.maxTurns, thread.kind == SocialThreadKind.QUESTION ? 5 : 4);
    }

    /// <summary>새벽 관찰실의 대사에 들어갈 실제 값들. 채울 수 없는 값은 넣지 않는다(그 값이 든 문장은 고르지 않게 된다).</summary>
    private static Dictionary<string, string> DawnSlots(SocialFacts facts, in ChatSituationRead read, Stock stock)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal);
        slots["stockName"] = GameLocale.Text(facts.stockName ?? facts.symbol ?? "");
        string shortName = ChatHypeAssets.ShortName?.Invoke(facts.symbol);
        if (!string.IsNullOrEmpty(shortName)) { slots["stockShort"] = shortName; }
        if (!string.IsNullOrEmpty(facts.price)) { slots["price"] = facts.price; }
        ChatDawnTalk.FillRead(read, slots);
        if (facts.held && !string.IsNullOrEmpty(facts.avgPrice))
        {
            slots["avg"] = facts.avgPrice;
            if (decimal.TryParse(facts.pct, NumberStyles.Number | NumberStyles.AllowLeadingSign, SocialCulture, out decimal result)) { slots["myPct"] = ChatSituationMath.SignedPercent(result); }
        }
        if (stock != null)
        {
            // 호가창에서 누구나 보는 큰 잔량. 약한 것은 벽이라고 부르지 않는다.
            var walls = ManipulationTools.Walls(stock);
            if (walls.Bid >= .2f && walls.BidPrice > 0m) { slots["bidWall"] = Money(walls.BidPrice); }
            if (walls.Ask >= .2f && walls.AskPrice > 0m) { slots["askWall"] = Money(walls.AskPrice); }
        }
        return slots;
    }

    /// <summary>
    /// 새벽 관찰실에서 나오는 말을 그 언어로 따로 쓴 이 방의 대사로 바꾼다. 바꿨으면 true이고 text가 화면에 뜰 글이다.
    /// 묶음은 말의 종류와 누구의 말에 다는 답인지, 그 종목의 상황, 말하는 사람의 처지(보유·수익·손실)와 견해(싸다·비싸다)로 고른다.
    /// 번역을 거치지 않으므로 번역용 정보(localizedPhrase 등)는 비운다. 고를 문장이 없으면 false(예전 길로 간다).
    /// </summary>
    private static bool TryDawnLine(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, IEnumerable<SocialMessage> recent, out string text)
    {
        text = null;
        if (message.roomId != NpcLeadingRoom.RoomId) { return false; }
        var facts = message.facts; string code = GameLocale.Code; var society = Instance;
        if (society == null || facts == null || facts.signal || facts.directLead || message.memory || template.variant == "memory" || template.promisedAction != SocialPromisedAction.None ||
            string.IsNullOrEmpty(facts.symbol) || !ChatDawnAssets.Has(code))
        { return false; }
        if (!string.IsNullOrEmpty(facts.notice) && facts.notice != "WATCH_RISE" && facts.notice != "WATCH_FALL") { return false; }
        society.SocialBots.TryGetValue(mind.SocialId, out var bot);
        var stock = society.FindSocialStock(facts.symbol);
        ChatSituationBoard.TryRead(facts.symbol, out var read);
        // 처지는 그 봇의 실제 보유와 평가 손익에서 읽는다.
        decimal result = 0m;
        bool holds = facts.held && facts.profitKind == "평가" && decimal.TryParse(facts.pct, NumberStyles.Number | NumberStyles.AllowLeadingSign, SocialCulture, out result);
        var stance = !holds ? ChatStance.Watch : result >= 0m ? ChatStance.Gain : ChatStance.Loss;
        string view = bot != null && stock != null ? ChatDawnTalk.View(bot.BelievedPrice(stock), stock.currentPrice) : "neutral";
        string parentView = parent != null && stock != null && society.SocialBots.TryGetValue(parent.senderId, out var author) ? ChatDawnTalk.View(author.BelievedPrice(stock), stock.currentPrice) : null;
        string group = ChatDawnTalk.Group(message.act, parent?.act, message.topicEvent == SocialMotive.SELF_EVENT.ToString(), ChatDawnTalk.Bucket(read.valid ? read.kind : ChatSituation.Quiet), stance, view, parentView, facts.bought, facts.sold, facts.held);
        if (group == null) { return false; }
        var slots = DawnSlots(facts, read, stock);
        ChatDawnTalk.NarrowForPlan(group, read, slots);
        bool Repeated(int index)
        {
            string speech = "dawn." + group + "." + index;
            if (mind.RecentSpeechKeys.Contains(speech)) { return true; }
            if (recent != null) { foreach (var previous in recent) { if (previous.debug?.speechKey == speech) { return true; } } }
            return false;
        }
        var random = CosmeticRandom(mind, message, 0x2c6e41d5u);
        string chosen = ChatDawnTalk.Pick(code, group, slots, random.Next, Repeated, out int number);
        if (chosen == null) { return false; }
        // 혼자 꺼내는 말은 여러 종목 얘기가 섞인 방에 뜬다. 종목 이름이 없는 문장 앞에는 이름을 붙인다. 답글은 무엇을 두고 하는 말인지 이미 안다.
        text = ChatHypeAssets.Fill(parent == null ? NamedLine(chosen) : chosen, slots);
        message.debug.speechKey = "dawn." + group + "." + number;
        message.debug.numberStyle = "새벽 관찰실(" + code + ")";
        message.localizedPhrase = null; message.localizedValues = null; message.localizedSuffix = ""; message.localizedTarget = null;
        return true;
    }
}
