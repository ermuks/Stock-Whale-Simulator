using System;
using System.Collections.Generic;
using UnityEngine;

// 라운지의 토론: 봇 몇이 한 주제를 두고 편을 갈라 말을 주고받는다. 오로지 보는 재미를 위한 것이다.
//
// 흐름은 정해져 있지 않다. 한 줄이 나올 때마다 다음 수를 뽑는다:
//   자기 편 주장 · 반박 · 농담 · 물음 · 시세를 든 말 · 인증 요구 · 아까 한 말 들추기 · 뒤늦게 온 사람의 "무슨 일임?" · 구경꾼의 한마디
//   · 달아오르면 서로 찌르기 · 누군가 말리기 · 삐져서 빠지기 · 손실 고백과 위로 · 옆길로 새기(다른 주제로) · 하던 얘기로 돌아가기
// 주제는 나무처럼 갈라진다. 주제마다 새기 쉬운 옆길이 있고, 가끔은 전혀 상관없는 데로 튄다. 새고 나면 편도 다시 섞인다.
//
// 플레이어가 끼어들면 판이 바뀐다:
//   종목을 말하면 그 종목 얘기로 넘어간다 · 맞장구/반대를 하면 그 편이 힘을 얻고 반대편이 내게 따진다 · 딴 얘기를 꺼내면 그리로 샌다
//   웃으면 달아오른 판이 식는다 · 막말을 하면 싸우던 봇들이 한편이 되고 얘기가 끊긴다 · 가만히 있으면 누가 내 생각을 묻는다
//
// 표시 전용이다. 방의 대화 기억·목격·감정·난수열·주문 어디에도 넣지 않는다. 종목 얘기의 편은 그 봇이 실제로 믿는 값으로 정한다.
// 대사는 언어마다 따로 쓴 파일(Chat/chat_debate.ko/en/ja)에서 온다.
[Serializable] public sealed class DebateTopic
{
    public string id;
    /// <summary>종목을 두고 하는 얘기인가(장중에, 종목이 있을 때만 꺼낸다).</summary>
    public bool stock;
    /// <summary>시장과 상관없는 잡담인가. 잡담에서는 인증을 요구하거나 지난 말을 들추지 않는다.</summary>
    public bool casual;
    public string[] open, pro, con, joke, ask, fact, branches;
    /// <summary>달아올랐을 때 가끔 나오는 긴 글(찬성 쪽, 반대 쪽). 경험담과 까닭이 든 글이다.</summary>
    public string[] rantPro, rantCon;
}
[Serializable] public sealed class DebateLocale
{
    public int schemaVersion = 1;
    public DebateTopic[] topics;
    public string[] bridge, back, heat, cool, quit, close, proof, callback, late, recap, popcorn, react, confess, comfort, rant;
    /// <summary>
    /// 잡담(점심, 날씨, 연애 …)에서 쓰는 찌르기·말리기·줄여 말하기·맺는말. 위의 것들은 주식 논쟁의 말이라
    /// ("지난번에도 틀렸잖아", "어차피 가격이 답해 줌", "둘 다 물림") 치킨 얘기에 나오면 딴소리가 된다.
    /// </summary>
    public string[] heatCasual, coolCasual, recapCasual, closeCasual;
    public string[] pull, playerSide, playerAgainst, playerDerail, playerCalm, playerUnite, playerStock;
    [NonSerialized] public Dictionary<string, DebateTopic> index;
}
public static class DebateAssets
{
    private static Dictionary<string, DebateLocale> locales;
    private static bool failed;
    public static bool Ready
    {
        get
        {
            if (locales == null && !failed)
            {
                var loaded = new Dictionary<string, DebateLocale>(StringComparer.Ordinal);
                foreach (string code in ChatHypeAssets.Languages)
                {
                    var asset = Resources.Load<TextAsset>("Chat/chat_debate." + code);
                    var locale = asset == null ? null : JsonUtility.FromJson<DebateLocale>(asset.text);
                    if (locale == null || locale.schemaVersion != 1 || locale.topics == null || locale.topics.Length == 0) { continue; }
                    locale.index = new Dictionary<string, DebateTopic>(StringComparer.Ordinal);
                    foreach (var topic in locale.topics) { if (topic != null && !string.IsNullOrEmpty(topic.id) && topic.open != null && topic.open.Length > 0) { locale.index[topic.id] = topic; } }
                    if (locale.index.Count > 0) { loaded[code] = locale; }
                }
                if (loaded.Count == 0) { failed = true; Debug.LogWarning("라운지 토론을 끕니다: chat_debate 파일이 없습니다."); } else { locales = loaded; }
            }
            return locales != null;
        }
    }
    public static DebateLocale Current
    {
        get
        {
            if (!Ready) { return null; }
            if (locales.TryGetValue(GameLocale.Code, out var locale) || locales.TryGetValue("ko", out locale)) { return locale; }
            foreach (var any in locales.Values) { return any; }
            return null;
        }
    }
}

public sealed partial class InvestorSociety
{
    private const int DebateRoom = 0;
    /// <summary>토론 한 판. cast는 봇 번호, side는 그 봇의 편(0 찬성, 1 반대, 2 농담꾼, 3 구경꾼).</summary>
    private sealed class Debate
    {
        public string topic; public readonly List<string> trail = new();
        /// <summary>이 판에서 이미 나온 말. 같은 말을 두 번 하지 않는다.</summary>
        public readonly List<string> used = new();
        public readonly List<int> cast = new(); public readonly List<int> side = new();
        public int turns, limit, lastSpeaker, lastSide = -1, sinceBranch, playerSide;
        /// <summary>바로 앞에 나온 말의 갈래(open, pro, ask, react …). 다음 말이 앞말과 이어지는지 가리는 데 쓴다.</summary>
        public string lastMove;
        /// <summary>토론이 받아치기로 한 내 글(답글이 가리킬 글).</summary>
        public long pendingFor;
        public long lastId; public double next, lastAt; public float heat;
        public Stock stock; public bool pulled, lateDone, confessed;
        /// <summary>이 주제에서 긴 글을 이미 쓴 편(찬성, 반대). 한 주제에서 한 편이 한 번만 길게 쓴다.</summary>
        public bool rantedPro, rantedCon;
        /// <summary>플레이어가 끼어든 뒤 다음 차례에 할 말(풀 이름)과 그 말을 할 편.</summary>
        public string pending; public int pendingSide = -1; public bool endAfterPending;
    }
    private Debate debate;
    private double debateNext;
    private readonly List<TradeBot> debateFolk = new();
    private readonly List<string> debateLines = new();
    /// <summary>확인용: 토론에서 나온 말과 흐름.</summary>
    public readonly Queue<string> DebateTrace = new(128);
    public int DebatesStarted { get; private set; }
    public int DebateLinesSaid { get; private set; }
    public int DebateBranches { get; private set; }
    public bool DebateRunning => debate != null;
    private void TraceDebate(string line) { DebateTrace.Enqueue(line); while (DebateTrace.Count > 120) { DebateTrace.Dequeue(); } }

    private void TickDebate()
    {
        if (day == null || !SocialReady || !DebateAssets.Ready || !ChatHypeAssets.Ready || StockManager.Instance == null) { return; }
        double now = SocialTime.Seconds;
        if (debate == null)
        {
            if (debateNext <= 0 || debateNext - now > 600) { debateNext = now + 20 + HypeRandom.Next() * 25; }
            // 급등락으로 방이 들끓는 동안에는 새 얘기를 꺼내지 않는다.
            if (now < debateNext || hypeRun != null) { return; }
            StartDebate(now);
            return;
        }
        if (Math.Abs(now - debate.lastAt) > 120) { debate = null; debateNext = now + 30; return; }   // 불러오기 등으로 시계가 튀었다.
        if (hypeRun != null)
        {
            // 급등락이 터지면 하던 얘기는 묻힌다.
            if (now - debate.lastAt > 20) { EndDebate(now, "급등락에 묻힘"); }
            return;
        }
        if (now >= debate.next) { AdvanceDebate(now); }
    }

    private bool DebateEligible(TradeBot bot, float clock, TradeBot odd, int formal)
        => bot != null && bot.Social != null && bot.isActiveAndEnabled && bot != odd && Present(bot, DebateRoom) && bot.Social.Psychology.ActiveAt(clock) && bot.Social.Psychology.Traits.Politeness < formal && !IsTyping(bot);

    private void StartDebate(double now)
    {
        var locale = DebateAssets.Current;
        debateFolk.Clear(); float clock = SessionClock.Time; var odd = ResolveOddball(); int formal = Config.Int("styleFormalMin");
        foreach (var bot in socialBots) { if (DebateEligible(bot, clock, odd, formal)) { debateFolk.Add(bot); } }
        if (locale == null || debateFolk.Count < 4) { debateNext = now + 20; return; }
        Stock subject = DebateStock();
        DebateTopic topic = null;
        for (int tries = 0; tries < 12 && topic == null; tries++)
        {
            var candidate = locale.topics[HypeRandom.Range(0, locale.topics.Length)];
            if (candidate == null || candidate.open == null || candidate.open.Length == 0 || candidate.stock && subject == null) { continue; }
            topic = candidate;
        }
        if (topic == null) { debateNext = now + 20; return; }
        var run = new Debate { topic = topic.id, limit = HypeRandom.Range(9, 22), stock = topic.stock ? subject : null, lastAt = now, lastSpeaker = 0 };
        int size = HypeRandom.Range(3, 6);
        for (int i = 0; i < size && debateFolk.Count > 0; i++)
        {
            int pick = HypeRandom.Range(0, debateFolk.Count); run.cast.Add(debateFolk[pick].Social.Id); run.side.Add(0); debateFolk.RemoveAt(pick);
        }
        debate = run; DebatesStarted++;
        AssignSides(run, topic);
        TraceDebate("== 토론 시작: " + topic.id + (run.stock != null ? " (" + run.stock.symbol + ")" : "") + " · " + run.cast.Count + "명 · " + run.limit + "줄 예정 ==");
        Speak(run, 0, topic.open, "open", now, 0);
    }
    /// <summary>편을 가른다. 종목 얘기면 그 봇이 실제로 믿는 값으로(싸다고 믿으면 찬성), 아니면 섞어서. 찬성과 반대가 적어도 하나씩은 있다.</summary>
    private void AssignSides(Debate run, DebateTopic topic)
    {
        for (int i = 0; i < run.cast.Count; i++)
        {
            int side = HypeRandom.Next() < .5 ? 0 : 1;
            if (topic.stock && run.stock != null && SocialBots.TryGetValue(run.cast[i], out var bot) && run.stock.currentPrice > 0m)
            {
                decimal edge = bot.BelievedPrice(run.stock) / run.stock.currentPrice;
                side = edge > 1.03m ? 0 : edge < .97m ? 1 : side;
            }
            if (i >= 2 && HypeRandom.Next() < .3) { side = HypeRandom.Next() < .6 ? 2 : 3; }
            run.side[i] = side;
        }
        if (run.cast.Count >= 2)
        {
            if (!run.side.Contains(0)) { run.side[0] = 0; }
            if (!run.side.Contains(1)) { run.side[run.side[0] == 0 ? 1 : 0] = 1; }
        }
    }
    private Stock DebateStock()
    {
        if (day.Phase != MarketPhase.Trading) { return null; }
        Stock best = null; float most = -1f;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || !stock.AcceptsOrders || stock.currentPrice <= 0m) { continue; }
            float weight = (StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock)) + (float)HypeRandom.Next() * 3f;
            if (weight > most) { most = weight; best = stock; }
        }
        return best;
    }

    private void AdvanceDebate(double now)
    {
        var run = debate; var locale = DebateAssets.Current;
        if (locale == null || !locale.index.TryGetValue(run.topic, out var topic)) { EndDebate(now, "주제를 잃음"); return; }
        // 자리를 뜬 봇은 판에서 빠진다.
        for (int i = run.cast.Count - 1; i >= 0; i--)
        { if (!SocialBots.TryGetValue(run.cast[i], out var member) || !Present(member, DebateRoom)) { run.cast.RemoveAt(i); run.side.RemoveAt(i); } }
        if (run.cast.Count < 2) { EndDebate(now, "사람이 빠짐"); return; }

        // 플레이어가 끼어든 뒤의 한마디가 먼저다.
        if (run.pending != null)
        {
            string name = run.pending; int wanted = run.pendingSide; bool end = run.endAfterPending;
            run.pending = null; run.pendingSide = -1; run.endAfterPending = false;
            int speaker = PickDebater(run, wanted);
            // 플레이어에게 하는 말은 내 글에 답글로 달고 나를 부른다. "무슨 일임?"에 대한 정리나 위로는 봇끼리의 말이다.
            bool mine = name.StartsWith("player", StringComparison.Ordinal) || name == "pull";
            bool spoke = speaker >= 0 && (name == "pull"
                ? Speak(run, speaker, locale.pull, "askBack.side", now, 0, toPlayer: true, asksPlayer: true)
                : Speak(run, speaker, DebatePool(locale, name), name, now, mine ? run.pendingFor : run.lastId, toPlayer: mine));
            run.pendingFor = 0;
            if (spoke && end) { EndDebate(now, "플레이어 때문에 끊김"); }
            return;
        }
        if (run.turns >= run.limit)
        {
            // 물음으로 끝내지 않는다: 마지막 말이 물음이었으면 누가 답하고 나서 끝난다.
            if (run.lastMove == "ask" && run.turns < run.limit + 2 && AnswerDebateAsk(run, topic, now)) { return; }
            string[] ending = topic.casual ? locale.closeCasual : locale.close;
            if (HypeRandom.Next() < .65) { Speak(run, PickDebater(run, -1), ending, "close", now, 0); }
            EndDebate(now, "할 말을 다 함");
            return;
        }
        // 누가 물었으면 다음 말은 그 물음에 대한 답이다("뭐 시켰음?" 다음에 "ㅇㅈ"이나 딴 물음이 오지 않게 한다).
        if (run.lastMove == "ask" && AnswerDebateAsk(run, topic, now)) { return; }
        int who = PickDebater(run, -1);
        if (who < 0) { EndDebate(now, "말할 사람이 없음"); return; }
        int side = run.side[who]; double roll = HypeRandom.Next();
        // 맞장구·구경꾼의 한마디는 받을 말(주장, 농담, 고백 …)이 앞에 있을 때만 나온다. 첫마디나 물음, 다른 맞장구 뒤에는 붙지 않는다.
        bool meaty = run.lastMove == "pro" || run.lastMove == "con" || run.lastMove == "joke" || run.lastMove == "fact" || run.lastMove == "rant" || run.lastMove == "proof" ||
            run.lastMove == "callback" || run.lastMove == "heat" || run.lastMove == "answer";
        bool playerNear = playerNote.spokeAt > 0 && now - playerNote.spokeAt < 240 && playerNote.room == DebateRoom && PlayerCanWrite(DebateRoom);

        // 옆길로 새기: 얘기가 좀 오간 뒤, 한 주제에 오래 머물수록 잘 샌다.
        if (run.turns >= 4 && run.sinceBranch >= 5 && roll < .04 + (run.sinceBranch - 5) * .025) { BranchDebate(run, locale, topic, who, now); return; }
        // 가만히 보고 있는 나를 끌어들인다.
        if (playerNear && !run.pulled && run.turns >= 2 && HypeRandom.Next() < .22 && side <= 1)
        {
            run.pulled = true; run.lastSide = side;
            // 물어 놓고는 내가 답할 틈을 준다. 그동안 얘기가 딴 데로 새지 않게 한다.
            if (Speak(run, who, locale.pull, "askBack.side", now, 0, toPlayer: true, asksPlayer: true)) { run.next += 4.5; run.sinceBranch = Math.Min(run.sinceBranch, 2); }
            return;
        }
        // 흥분하면 길게 쓴다: 대부분은 한두 마디지만, 판이 달아오르면 누군가 자기 경험과 까닭을 길게 풀어 놓는다.
        if (run.heat >= 2f && side <= 1 && !(side == 0 ? run.rantedPro : run.rantedCon) && run.lastSide >= 0 && run.lastSide != side && HypeRandom.Next() < .6)
        {
            string[] own = side == 0 ? topic.rantPro : topic.rantCon;
            string[] lengthy = own != null && own.Length > 0 && (topic.casual || HypeRandom.Next() < .75) ? own : topic.casual ? null : locale.rant;
            if (lengthy != null && lengthy.Length > 0 && Speak(run, who, lengthy, "rant", now, run.lastId, whole: true))
            {
                if (side == 0) { run.rantedPro = true; } else { run.rantedCon = true; }
                run.heat += 1.5f; TraceDebate("    (" + Nick(run.cast[who]) + " 흥분해서 길게 씀)");
                return;
            }
        }
        // 달아오른 판: 찌르고, 말리고, 삐져서 빠진다.
        if (run.heat >= 3f && side <= 1 && HypeRandom.Next() < .4)
        {
            if (run.heat >= 5.5f && HypeRandom.Next() < .35)
            {
                if (run.cast.Count > 2 && HypeRandom.Next() < .5)
                {
                    if (Speak(run, who, locale.quit, "quit", now, run.lastId)) { TraceDebate("    (" + Nick(run.cast[who]) + " 삐져서 빠짐)"); run.cast.RemoveAt(who); run.side.RemoveAt(who); run.lastSpeaker = -1; run.heat -= 2f; }
                    return;
                }
                int calm = PickDebater(run, 3); if (calm < 0) { calm = PickDebater(run, 2); }
                if (calm >= 0 && Speak(run, calm, topic.casual ? locale.coolCasual : locale.cool, "cool", now, 0)) { run.heat = Mathf.Max(0f, run.heat - 3f); return; }
            }
            if (Speak(run, who, topic.casual ? locale.heatCasual : locale.heat, "heat", now, run.lastId)) { run.heat += 1f; return; }
        }
        // 뒤늦게 들어온 사람이 무슨 일이냐고 묻고, 누가 줄여서 알려 준다.
        if (!run.lateDone && run.turns >= 5 && HypeRandom.Next() < .1)
        {
            run.lateDone = true;
            var late = Newcomer(run);
            if (late != null)
            {
                run.cast.Add(late.Social.Id); run.side.Add(3);
                if (Speak(run, run.cast.Count - 1, locale.late, "late", now, 0)) { run.pending = topic.casual ? "recapCasual" : "recap"; run.pendingSide = -1; return; }
            }
        }
        // 손실 고백과 위로: 싸우다가도 누가 물렸다고 하면 잠깐 분위기가 바뀐다.
        // 잡담에서는 하지 않는다(점심 얘기 도중의 "솔직히 오늘 좀 잃었음"은 딴소리다).
        if (!run.confessed && !topic.casual && run.turns >= 4 && HypeRandom.Next() < .07)
        {
            run.confessed = true;
            if (Speak(run, who, locale.confess, "confess", now, 0)) { run.pending = "comfort"; run.pendingSide = -1; run.heat = Mathf.Max(0f, run.heat - 1.5f); return; }
        }
        string move; string[] pool;
        // 한 단어 반응: 실제 채팅방처럼 "ㅋㅋ", "ㄹㅇ" 한마디가 사이사이 낀다.
        if (meaty && HypeRandom.Next() < (side >= 2 ? .3 : .1) && locale.react != null && locale.react.Length > 0)
        { if (Speak(run, who, locale.react, "react", now, run.lastId)) { run.next = now + 1.2 + HypeRandom.Next() * 1.8; } return; }
        // 끝나기 직전에는 묻지 않는다(답할 틈이 없다).
        bool mayAsk = run.turns < run.limit - 1;
        if (side == 3 && meaty) { move = "popcorn"; pool = locale.popcorn; }
        else if (side >= 2) { move = HypeRandom.Next() < .7 || !mayAsk ? "joke" : "ask"; pool = move == "joke" ? topic.joke : topic.ask; }
        else if (roll > .9 && mayAsk && topic.ask != null && topic.ask.Length > 0) { move = "ask"; pool = topic.ask; }
        else if (roll > .8 && run.stock != null && topic.fact != null && topic.fact.Length > 0) { move = "fact"; pool = topic.fact; }
        else if (roll > .72 && meaty && !topic.casual && run.lastSide >= 0 && run.lastSide != side && run.lastSide <= 1) { move = "proof"; pool = locale.proof; }
        else if (roll > .65 && meaty && !topic.casual && run.turns >= 6 && run.trail.Count > 0 && run.lastSide <= 1 && run.lastSide != side) { move = "callback"; pool = locale.callback; }
        else { move = side == 0 ? "pro" : "con"; pool = side == 0 ? topic.pro : topic.con; }
        if (pool == null || pool.Length == 0) { move = side == 0 ? "pro" : "con"; pool = side == 0 ? topic.pro : topic.con; }
        bool rebuts = side <= 1 && run.lastSide >= 0 && run.lastSide <= 1 && run.lastSide != side;
        if (!Speak(run, who, pool, move, now, rebuts || move == "proof" || move == "callback" ? run.lastId : 0))
        {
            // 이 주제에서 할 말을 다 했다. 농담이나 물음으로 돌리고, 그것도 없으면 다른 얘기로 넘어간다.
            if (Speak(run, who, topic.joke, "joke", now, 0) || Speak(run, who, topic.ask, "ask", now, 0)) { run.heat = Mathf.Max(0f, run.heat - .4f); return; }
            if (run.turns < run.limit - 2) { BranchDebate(run, locale, topic, who, now); }
            if (run.lastAt < now) { run.turns = run.limit; run.next = now + 1.5; }
            return;
        }
        if (move == "joke" || move == "popcorn") { run.heat = Mathf.Max(0f, run.heat - .4f); }
        else if (rebuts) { run.heat += move == "proof" || move == "callback" ? 1.4f : 1f; }
        // 내가 편을 든 쪽이 말하면 기세가 오른다.
        if (run.playerSide != 0 && side == (run.playerSide > 0 ? 0 : 1)) { run.heat += .2f; }
    }

    /// <summary>
    /// 방금 나온 물음에 누가 답한다: 찬반 어느 한쪽의 사람이 자기 주장으로 받는다(물은 사람의 글에 답글로 단다).
    /// 답할 사람이나 남은 말이 없으면 false(그때는 평소대로 다음 말을 고른다).
    /// </summary>
    private bool AnswerDebateAsk(Debate run, DebateTopic topic, double now)
    {
        int who = PickDebater(run, HypeRandom.Next() < .5 ? 0 : 1);
        if (who < 0 || run.side[who] > 1) { who = PickDebater(run, 0); }
        if (who < 0 || run.side[who] > 1) { who = PickDebater(run, 1); }
        if (who < 0 || run.side[who] > 1) { run.lastMove = null; return false; }
        string[] pool = run.side[who] == 0 ? topic.pro : topic.con;
        if (run.stock != null && topic.fact != null && topic.fact.Length > 0 && HypeRandom.Next() < .3) { pool = topic.fact; }
        if (Speak(run, who, pool, "answer", now, run.lastId)) { return true; }
        run.lastMove = null; return false;
    }
    private void BranchDebate(Debate run, DebateLocale locale, DebateTopic topic, int who, double now)
    {
        // 돌아가기: 새기 전에 하던 얘기가 있으면 가끔 그리로 돌아간다.
        if (run.trail.Count > 0 && HypeRandom.Next() < .18)
        {
            string earlier = run.trail[run.trail.Count - 1];
            if (locale.index.TryGetValue(earlier, out var old) && (!old.stock || run.stock != null || (run.stock = DebateStock()) != null))
            {
                run.trail.RemoveAt(run.trail.Count - 1); run.topic = earlier; run.sinceBranch = 0; run.heat *= .5f; DebateBranches++;
                TraceDebate("    (하던 얘기로 돌아감: " + earlier + ")");
                Speak(run, who, locale.back, "back", now, 0, tail: Pick(old.pro, old.con));
                return;
            }
        }
        DebateTopic next = null;
        for (int tries = 0; tries < 10 && next == null; tries++)
        {
            // 대개는 그 주제에서 새기 쉬운 옆길로, 가끔은 전혀 상관없는 데로.
            string id = topic.branches != null && topic.branches.Length > 0 && HypeRandom.Next() < .7 ? topic.branches[HypeRandom.Range(0, topic.branches.Length)] : locale.topics[HypeRandom.Range(0, locale.topics.Length)].id;
            if (id == run.topic || run.trail.Contains(id) || !locale.index.TryGetValue(id, out var candidate)) { continue; }
            if (candidate.stock && run.stock == null && (run.stock = DebateStock()) == null) { continue; }
            next = candidate;
        }
        if (next == null) { run.sinceBranch = 0; return; }
        run.trail.Add(run.topic); run.topic = next.id; run.sinceBranch = 0; run.heat *= .5f; DebateBranches++;
        if (!next.stock) { run.stock = null; }
        // 얘기가 바뀌면 편도 다시 갈린다. 방금까지 싸우던 둘이 같은 편이 되기도 한다.
        AssignSides(run, next); run.playerSide = 0; run.pulled = false; run.rantedPro = run.rantedCon = false;
        run.limit = Math.Max(run.limit, run.turns + HypeRandom.Range(4, 10));
        TraceDebate("    (옆길로 샘: " + next.id + ")");
        Speak(run, who, locale.bridge, "branch", now, 0, tail: next.open[HypeRandom.Range(0, next.open.Length)]);
    }
    private string Pick(string[] first, string[] second)
    {
        var pool = first != null && first.Length > 0 && (second == null || second.Length == 0 || HypeRandom.Next() < .5) ? first : second;
        return pool == null || pool.Length == 0 ? null : pool[HypeRandom.Range(0, pool.Length)];
    }
    private static string[] DebatePool(DebateLocale locale, string name) => name switch
    {
        "recap" => locale.recap, "recapCasual" => locale.recapCasual, "comfort" => locale.comfort, "playerSide" => locale.playerSide, "playerAgainst" => locale.playerAgainst, "playerDerail" => locale.playerDerail,
        "playerCalm" => locale.playerCalm, "playerUnite" => locale.playerUnite, "playerStock" => locale.playerStock, "pull" => locale.pull, _ => null
    };
    private string Nick(int id) => SocialBots.TryGetValue(id, out var bot) && bot.Social != null ? bot.Social.Nickname : "?";
    /// <summary>다음에 말할 사람. 방금 말한 사람은 빼고, side를 주면 그 편에서 고른다(없으면 아무나).</summary>
    private int PickDebater(Debate run, int side)
    {
        int found = -1, seen = 0;
        for (int pass = 0; pass < 2 && found < 0; pass++)
        {
            for (int i = 0; i < run.cast.Count; i++)
            {
                if (run.cast[i] == run.lastSpeaker || pass == 0 && side >= 0 && run.side[i] != side) { continue; }
                if (!SocialBots.TryGetValue(run.cast[i], out var bot) || IsTyping(bot)) { continue; }
                seen++; if (HypeRandom.Range(0, seen) == 0) { found = i; }
            }
        }
        return found;
    }
    /// <summary>판에 없던 사람 하나(뒤늦게 온 사람).</summary>
    private TradeBot Newcomer(Debate run)
    {
        debateFolk.Clear(); float clock = SessionClock.Time; var odd = ResolveOddball(); int formal = Config.Int("styleFormalMin");
        foreach (var bot in socialBots) { if (DebateEligible(bot, clock, odd, formal) && !run.cast.Contains(bot.Social.Id)) { debateFolk.Add(bot); } }
        return debateFolk.Count == 0 ? null : debateFolk[HypeRandom.Range(0, debateFolk.Count)];
    }
    /// <summary>토론의 한 줄을 올린다. tail은 뒤에 이어 붙일 말(옆길로 샐 때 새 주제의 첫마디).</summary>
    private bool Speak(Debate run, int who, string[] pool, string move, double now, long replyTo, string tail = null, bool toPlayer = false, bool asksPlayer = false, bool whole = false)
    {
        if (who < 0 || who >= run.cast.Count || pool == null || pool.Length == 0 || !SocialBots.TryGetValue(run.cast[who], out var bot)) { return false; }
        string target = run.lastSpeaker != 0 && run.lastSpeaker != run.cast[who] ? Nick(run.lastSpeaker) : null;
        bool hasStock = run.stock != null && run.stock.currentPrice > 0m;
        debateLines.Clear();
        foreach (string line in pool)
        {
            if (string.IsNullOrEmpty(line)) { continue; }
            if (!hasStock && (line.Contains("{stockName}") || line.Contains("{price}") || line.Contains("{dayPct}")) || target == null && line.Contains("{who}")) { continue; }
            // 긴 글에 종목 빈칸이 있으면 종목이 있어야 한다(위에서 걸렀다).
            // 한 단어 반응은 되풀이돼도 되지만, 주장과 농담은 한 판에 한 번만 나온다.
            if (move != "react" && run.used.Contains(line)) { continue; }
            debateLines.Add(line);
        }
        string text = PickHypeText(debateLines.ToArray(), Array.Empty<string>());
        if (text == null) { return false; }
        // 이어 붙인 말도 방금 나온 말로 쳐서, 다음 사람이 똑같은 말을 되풀이하지 않게 한다.
        run.used.Add(text);
        if (tail != null) { text = text + " " + tail; hypeRecentLines.Enqueue(tail); run.used.Add(tail); }
        var slots = hasStock ? ChartSlots(run.stock, run.stock.currentPrice, run.stock.currentPrice, 0m) : HypeSlots(null, 0, 0m);
        slots["who"] = target == null ? "" : "@" + target; slots["you"] = YouMention;
        string said = ChatHypeAssets.Fill(text, slots);
        if (toPlayer && said.IndexOf(YouMention, StringComparison.Ordinal) < 0) { said = YouMention + " " + said; }
        // 멘션은 문장과 이어 붙이지 않는다("@당신은 …" → "@당신 님은 …").
        said = DetachMention(said);
        // 내게 묻는 말만 "플레이어에게 한 물음"으로 남긴다(내가 답하면 물은 봇이 받는다). 나머지는 토론의 말이다.
        // 바로 올리지 않는다. 글 길이만큼 "입력 중"이 뜬 뒤에 올라간다. 봇끼리의 짧은 말은 그 봇의 버릇대로 끊어 치고, 내게 하는 말과 긴 글은 한 덩어리로 올린다.
        double doneAt = QueueLine(bot, DebateRoom, said, (asksPlayer ? "talk.debate." : "debate.") + run.topic + "." + move, hasStock ? run.stock : null, replyTo,
            asksPlayer ? "PLAYER_TALK" : "DEBATE", now, split: !asksPlayer && !toPlayer && !whole, owner: run);
        hypeSpokeAt[bot.Social.Id] = now;
        run.lastSpeaker = run.cast[who]; run.lastSide = run.side[who]; run.lastMove = move; run.turns++; run.sinceBranch++; run.lastAt = now; DebateLinesSaid++;
        // 긴 말은 읽는 데 시간이 걸린다.
        // 다음 사람은 이 글이 올라온 것을 읽고 나서 쓰기 시작한다. 긴 글은 읽는 데도 시간이 걸린다.
        run.next = doneAt + 1.2 + HypeRandom.Next() * 2.2 + Math.Min(4.0, said.Length * .035);
        TraceDebate("    " + bot.Social.Nickname + ": " + said + "  [" + run.topic + "." + move + " · " + (run.side[who] == 0 ? "찬" : run.side[who] == 1 ? "반" : run.side[who] == 2 ? "농" : "구") + " · 열 " + run.heat.ToString("0.0") + "]");
        return true;
    }
    private void EndDebate(double now, string why)
    {
        if (debate == null) { return; }
        TraceDebate("== 토론 끝: " + why + " · " + debate.turns + "줄 · 지나온 주제 " + (debate.trail.Count + 1) + "개 ==");
        debate = null;
        debateNext = now + 35 + HypeRandom.Next() * 55;
    }

    // ── 플레이어가 끼어든다 ──
    /// <summary>
    /// 토론 중에 내가 한 말이 판을 어떻게 바꾸는가. true면 토론이 이 말을 받았으니 평소의 답글은 달지 않는다.
    /// 토론이 없을 때 내가 말을 꺼내면 곧 누군가 얘기를 시작한다(조용한 방에 혼자 떠들게 두지 않는다).
    /// early: 내 말이 봇의 물음에 대한 답인지 가리기 전에 먼저 부른다. 종목 얘기·막말·웃음처럼 답이든 아니든 판을 바꾸는 말만 여기서 본다.
    /// </summary>
    private bool DebateHears(int room, PlayerTalk talk, double now, bool early = false)
    {
        // 토론이 내 말을 받아 한마디 하기로 했으면, 그 한마디가 가리킬 내 글을 적어 둔다.
        // (나중에 올라갈 때 "가장 최근의 내 글"을 가리키면, 그 사이에 내가 쓴 딴 글에 답한 것처럼 보인다.)
        string before = debate?.pending;
        bool heard = DebateHearsInner(room, talk, now, early);
        if (debate != null && debate.pending != null && debate.pending != before) { debate.pendingFor = playerNote.messageId; }
        return heard;
    }
    private bool DebateHearsInner(int room, PlayerTalk talk, double now, bool early)
    {
        if (room != DebateRoom || !DebateAssets.Ready) { return false; }
        if (debate == null) { if (early) { return false; } if (debateNext - now > 18) { debateNext = now + 8 + HypeRandom.Next() * 10; } return false; }
        var run = debate; var locale = DebateAssets.Current;
        if (locale == null || now - run.lastAt > 30) { return false; }
        run.limit = Math.Min(run.limit + 3, run.turns + 14); run.next = Math.Min(run.next, now + 1.6 + HypeRandom.Next());
        int towards = run.lastSide == 0 || run.lastSide == 1 ? run.lastSide : 0;
        switch (talk.Kind)
        {
            case PlayerTalkKind.Insult:
                // 싸우던 봇들이 한편이 되고, 얘기는 거기서 끊긴다. 핀잔은 평소대로 따로 달린다.
                run.heat = 0f; run.pending = "playerUnite"; run.pendingSide = -1; run.endAfterPending = true; run.next = now + 3.5;
                TraceDebate("    (플레이어의 막말: 판이 깨짐)");
                return false;
            case PlayerTalkKind.Mention: case PlayerTalkKind.ClaimUp: case PlayerTalkKind.ClaimDown: case PlayerTalkKind.AskOpinion: case PlayerTalkKind.AskBuy: case PlayerTalkKind.AskSell: case PlayerTalkKind.AskWhy:
            {
                // 종목 얘기는 먼저 부를 때(early) 한 번만 받는다(나중 부름에서 또 받으면 얘깃거리가 두 번 바뀐다).
                if (!early || talk.Stock == null || !talk.Stock.AcceptsOrders) { return false; }
                // 내가 꺼낸 종목 얘기로 넘어간다.
                DebateTopic next = null; int seen = 0;
                foreach (var candidate in locale.topics) { if (candidate != null && candidate.stock && candidate.id != run.topic && locale.index.ContainsKey(candidate.id)) { seen++; if (HypeRandom.Range(0, seen) == 0) { next = candidate; } } }
                if (next == null) { return false; }
                run.trail.Add(run.topic); run.topic = next.id; run.stock = talk.Stock; run.sinceBranch = 0; run.heat *= .5f; DebateBranches++;
                AssignSides(run, next);
                run.playerSide = talk.Kind == PlayerTalkKind.ClaimUp ? 1 : talk.Kind == PlayerTalkKind.ClaimDown ? -1 : 0;
                run.pending = "playerStock"; run.pendingSide = -1;
                TraceDebate("    (플레이어가 " + talk.Stock.symbol + " 얘기를 꺼냄: " + next.id + "로 넘어감)");
                return false;
            }
            case PlayerTalkKind.Agree: case PlayerTalkKind.Disagree:
                // "그렇구나"·"알겠음"은 알아들었다는 말이지 어느 편을 드는 말이 아니다.
                if (early || talk.Acks) { return false; }
                // 방금 말한 쪽에 맞장구를 치거나 반대한다. 그 편이 힘을 얻고, 반대편은 내게 따진다.
                DebatePlayerSide(talk.Kind == PlayerTalkKind.Agree ? towards == 0 : towards != 0, now);
                return true;
            case PlayerTalkKind.Topic:
            {
                if (early) { return false; }
                string id = talk.Topic == "work" ? "boss" : talk.Topic == "rich" ? "quit" : talk.Topic == "luck" ? "lotto" : talk.Topic == "who" ? "botOrNot" : talk.Topic == "drink" ? "lunch" : talk.Topic;
                if (id == null || id == run.topic || !locale.index.TryGetValue(id, out var next) || next.stock) { return false; }
                run.trail.Add(run.topic); run.topic = id; run.stock = null; run.sinceBranch = 0; run.heat *= .4f; DebateBranches++;
                AssignSides(run, next); run.playerSide = 0;
                run.pending = "playerDerail"; run.pendingSide = -1;
                TraceDebate("    (플레이어가 딴 얘기를 꺼냄: " + id + "로 샘)");
                // 토론이 그 얘기로 넘어가더라도 누군가는 내 말을 직접 받아 준다(예전에는 묻는 말일 때만 받아서, "치킨 먹고 싶다"에는 답이 없었다).
                return false;
            }
            case PlayerTalkKind.Laugh: case PlayerTalkKind.Surprise:
                if (run.heat >= 2f) { run.heat = Mathf.Max(0f, run.heat - 2.5f); run.pending = "playerCalm"; run.pendingSide = -1; TraceDebate("    (플레이어가 웃음: 판이 식음)"); return true; }
                return false;
            case PlayerTalkKind.Statement: case PlayerTalkKind.Other:
                if (early) { return false; }
                // 뜻을 알아듣지 못한 말에는 토론이 반응하지 않는다(예전에는 이 말을 빌미로 "생각은 어떰?" 하고 끌어들였다).
                return false;
        }
        return false;
    }
    /// <summary>내가 한쪽 편을 들었다(pro면 찬성 쪽).</summary>
    private void DebatePlayerSide(bool pro, double now)
    {
        if (debate == null) { return; }
        var run = debate;
        run.playerSide = pro ? 1 : -1; run.heat += 1f; run.limit = Math.Min(run.limit + 3, run.turns + 14);
        // 먼저 내 편이 반기고, 그다음 반대편이 내게 따진다.
        run.pending = HypeRandom.Next() < .5 ? "playerSide" : "playerAgainst"; run.pendingSide = run.pending == "playerSide" ? (pro ? 0 : 1) : (pro ? 1 : 0);
        run.next = Math.Min(run.next, now + 1.5 + HypeRandom.Next()); run.pendingFor = playerNote.messageId;
        TraceDebate("    (플레이어가 " + (pro ? "찬성" : "반대") + " 편을 듦)");
    }
}
