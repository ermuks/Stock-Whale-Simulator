using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// 플레이어의 채팅과 종목 분석 AI.
//
// 플레이어가 채팅방에 글을 쓰면, 글에서 뜻을 읽고(PlayerTalk.Read) 그 방에 있는 봇들이 사람처럼 받아친다.
// 언어 모델은 쓰지 않는다. 낱말과 말꼬리, 종목 이름, 숫자를 규칙으로 읽는다: 부르기(님들·여러분), 인사, 웃음(ㅋㅋ·ㅎㅎ·^^), 종목 묻기(살까·팔까·어때),
// 오른다·내린다는 주장, 수익 자랑과 손실 한탄, 고맙다·미안하다, 시비, "내가 세력" 같은 허세, 그리고 "!분석 종목" 명령.
// 봇의 답은 그 봇의 기분(욕심·공포), 나에 대한 믿음과 적대심, 그 종목을 보는 자기 생각(믿는 값)에 따라 달라진다.
// 답글은 화면 표시용이다(방의 대화 기억·난수열에는 넣지 않는다). 대신 내 말은 봇의 마음에 남는다:
//  - 수익 자랑은 내 실제 계좌로 확인된다. 인증되면 믿음이 오르고 배 아픈 봇은 적대심이 오른다. 인증이 안 되면 믿음이 깎인다.
//  - "오른다"·"내린다"는 말은 기억됐다가 몇 분 뒤 맞았는지 따진다. 맞으면 믿음이, 틀리면 적대심이 쌓인다.
//  - 믿음이 쌓인 봇일수록 내가 말한 종목으로 눈을 돌린다(ChatInfluence와 같은 장치).
// 주문을 대신 내거나 봇의 믿는 값을 바꾸지는 않는다. 실제 시청자 채팅은 이 길을 지나지 않는다.
//
// 종목 분석 AI: 새벽 관찰실에만 있다. "!분석 종목"이라고 쓰면(봇도 가끔 쓴다) 당일 시가·현재가·고가·저가와 매수 추천 점수를 카드로 올린다.
// 점수는 누구나 보는 시세·호가·거래량만으로 낸다. 진짜 가치는 읽지 않는다. 참고용이고 틀릴 수 있다. 봇은 AI 말대로 움직이지 않는다.

public enum PlayerTalkKind { Other, Analyze, Insult, Brag, Lament, AskBuy, AskSell, AskOpinion, AskPick, ClaimUp, ClaimDown, Whale, Thanks, Apology, Greeting, Bye, Laugh, Sad, CallAll }

public struct PlayerTalk
{
    public PlayerTalkKind Kind;
    public Stock Stock;
    /// <summary>글에 적힌 수익률(%). 없으면 NaN.</summary>
    public float Percent;
    public bool CallsAll, Laughs, Cries, Asks;
    /// <summary>"!분석" 뒤에 적은 말(종목을 못 찾았을 때 되돌려 보여 준다).</summary>
    public string Argument;

    private static readonly Regex PercentPattern = new(@"([+\-]?\d+(?:\.\d+)?)\s*(?:%|퍼|프로|パー)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LaughPattern = new(@"ㅋ{2,}|ㅎ{2,}|\^\^|\^_\^|lol|lmao|haha|ｗ{2,}|w{3,}|草", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CryPattern = new(@"ㅠ{2,}|ㅜ{2,}|ㅠㅜ|ㅜㅠ|하\.{2,}|흑흑|;;|orz|泣", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly string[] All = { "님들", "여러분", "저기요", "다들", "얘들아", "형들", "형님들", "누구", "아무나", "guys", "everyone", "anyone", "anybody", "y'all", "みんな", "皆さん", "すみません", "誰か" };
    private static readonly string[] Greet = { "안녕", "ㅎㅇ", "하이", "반갑", "반가워", "방가", "좋은아침", "굿모닝", "hello", "hi ", "hey", "good morning", "こんにちは", "おはよう", "こんばんは", "よろしく" };
    private static readonly string[] Farewell = { "잘자", "퇴근", "수고", "나간다", "이만", "빠이", "ㅂㅂ", "ㅂㅇ", "bye", "good night", "gn", "お疲れ", "おやすみ", "じゃあね" };
    private static readonly string[] Thank = { "감사", "고마워", "고맙", "ㄱㅅ", "땡큐", "thanks", "thank you", "thx", "ty", "ありがと", "感謝" };
    private static readonly string[] Sorry = { "미안", "죄송", "ㅈㅅ", "sorry", "my bad", "ごめん", "すまん" };
    private static readonly string[] Rude = { "바보", "멍청", "호구", "개미들", "닥쳐", "꺼져", "ㅂㅅ", "병신", "한심", "찐따", "루저", "흑우", "idiot", "stupid", "noob", "loser", "shut up", "dumb", "バカ", "馬鹿", "アホ", "雑魚", "黙れ" };
    private static readonly string[] BragWords = { "수익", "익절", "먹었", "먹음", "벌었", "벌음", "땄", "수익률", "플러스", "빨간불", "대박", "떡상했", "profit", "made ", "gains", "up ", "banked", "利確", "儲か", "勝った", "爆益" };
    private static readonly string[] LossWords = { "물렸", "물림", "손절", "잃었", "잃음", "마이너스", "파란불", "깡통", "손실", "털렸", "반토막", "bagholding", "lost ", "loss", "down ", "rekt", "stopped out", "損切", "含み損", "負けた", "爆損" };
    private static readonly string[] BuyAsk = { "살까", "사도", "사야", "탈까", "타도", "들어가도", "들어갈까", "진입", "매수각", "줍줍", "담을까", "should i buy", "buy now", "get in", "worth buying", "買い?", "買う?", "買っていい", "入っていい", "買い時" };
    private static readonly string[] SellAsk = { "팔까", "팔아야", "던질까", "던져야", "손절할까", "익절할까", "정리할까", "매도각", "should i sell", "sell now", "take profit", "get out", "売り?", "売る?", "売っていい", "利確していい", "売り時" };
    private static readonly string[] OpinionAsk = { "어때", "어떰", "어떻게 봐", "어떻게 보", "어케 봄", "어떨까", "괜찮", "갈까", "오를까", "떨어질까", "전망", "의견", "what do you think", "thoughts", "how about", "any good", "どう思う", "どう?", "どうかな", "どうよ", "見通し" };
    private static readonly string[] PickAsk = { "뭐 살", "뭐 사", "뭘 사", "뭐 타", "추천", "종목 좀", "뭐가 좋", "어디 들어", "what to buy", "any picks", "recommend", "what should i buy", "何買", "おすすめ", "何がいい" };
    private static readonly string[] UpWords = { "오른다", "올라간다", "간다", "쏜다", "쏠", "떡상", "상한가", "가즈아", "풀매수", "날아간다", "터진다", "급등", "올린다", "to the moon", "going up", "mooning", "pump", "rip", "sends", "上がる", "爆上げ", "ストップ高", "飛ぶ", "買い増し" };
    private static readonly string[] DownWords = { "떨어진다", "내려간다", "빠진다", "떡락", "하한가", "망했", "끝났", "폭락", "급락", "무너진다", "던져라", "going down", "dump", "crash", "tank", "it's over", "dead", "下がる", "暴落", "ストップ安", "終わった", "逃げろ" };
    private static readonly string[] WhaleWords = { "내가 세력", "나 세력", "제가 세력", "세력이다", "내가 올린", "내가 올릴", "작전 들어", "내가 쏜다", "i'm the whale", "i am the whale", "i'll pump", "i move this", "俺が仕手", "仕手だ", "俺が上げる" };

    private static bool Has(string text, string[] words)
    {
        foreach (string word in words) { if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) { return true; } }
        return false;
    }
    private static string Squeeze(string text) => (text ?? "").Replace(" ", "").Replace("　", "").ToLowerInvariant();

    /// <summary>
    /// 이름으로 종목을 찾는다. 화면에 보이는 이름(지금 언어), 원래 이름, 종목 기호 가운데 어느 것이든 된다.
    /// 다 쓰지 않아도 된다: "노보"처럼 앞머리 두 글자 이상이면 그 종목으로 알아듣는다. 여럿이 걸리면 가장 길게 겹치는 종목을 고른다.
    /// </summary>
    public static Stock FindStock(string words, bool allowShort)
    {
        var manager = StockManager.Instance; string text = Squeeze(words);
        if (manager == null || text.Length == 0) { return null; }
        Stock best = null; int bestLength = 0;
        foreach (var stock in manager.stocks)
        {
            if (stock == null || !stock.IsListed || stock.IsDelisted) { continue; }
            foreach (string raw in new[] { GameLocale.Text(stock.stockName), stock.stockName, stock.symbol })
            {
                string name = Squeeze(raw);
                if (name.Length < 2) { continue; }
                // 이름이 통째로 들어 있으면 그만큼, 아니면 이름의 앞머리가 글 속의 낱말로 쓰였으면 그 길이만큼 겹친 것으로 친다.
                int length = text.Contains(name) ? name.Length + 100 : 0;
                if (length == 0 && allowShort)
                {
                    for (int size = Math.Min(name.Length - 1, 8); size >= 2; size--)
                    { if (text.Contains(name.Substring(0, size))) { length = size; break; } }
                }
                if (length > bestLength) { bestLength = length; best = stock; }
            }
        }
        return best;
    }

    /// <summary>글 한 줄에서 뜻을 읽는다. 여러 뜻이 겹치면 더 무거운 쪽을 고른다: 명령 > 시비 > 자랑·한탄 > 묻기 > 주장 > 인사치레 > 웃음 > 부르기.</summary>
    public static PlayerTalk Read(string text)
    {
        var talk = new PlayerTalk { Percent = float.NaN, Kind = PlayerTalkKind.Other };
        string line = (text ?? "").Trim(); string lower = line.ToLowerInvariant();
        if (line.Length == 0) { return talk; }
        foreach (string command in new[] { "!분석", "！분석", "!analyze", "!analyse", "!analysis", "!分析", "！分析" })
        {
            if (!lower.StartsWith(command, StringComparison.Ordinal)) { continue; }
            talk.Kind = PlayerTalkKind.Analyze; talk.Argument = line.Substring(command.Length).Trim(); talk.Stock = FindStock(talk.Argument, true);
            return talk;
        }
        talk.CallsAll = Has(lower, All); talk.Laughs = LaughPattern.IsMatch(line); talk.Cries = CryPattern.IsMatch(line);
        talk.Asks = line.Contains("?") || line.Contains("？") || lower.EndsWith("냐") || lower.EndsWith("까") || lower.EndsWith("나요") || lower.EndsWith("ㅁ?");
        // 종목은 먼저 온전한 이름으로 찾고, 없으면 앞머리로 찾는다(세 글자 이상 겹칠 때만: "미래에는" 같은 말에 걸리지 않게).
        talk.Stock = FindStock(line, false);
        if (talk.Stock == null) { var loose = FindStock(line, true); if (loose != null && Overlap(line, loose) >= 3) { talk.Stock = loose; } }
        var match = PercentPattern.Match(line);
        if (match.Success && float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float percent)) { talk.Percent = percent; }

        bool loss = Has(lower, LossWords) || !float.IsNaN(talk.Percent) && talk.Percent < 0f, gain = Has(lower, BragWords) || !float.IsNaN(talk.Percent) && talk.Percent > 0f && !talk.Asks;
        if (Has(lower, Rude)) { talk.Kind = PlayerTalkKind.Insult; }
        else if (Has(lower, WhaleWords)) { talk.Kind = PlayerTalkKind.Whale; }
        else if (loss && !talk.Asks) { talk.Kind = PlayerTalkKind.Lament; }
        else if (gain && !talk.Asks) { talk.Kind = PlayerTalkKind.Brag; }
        else if (Has(lower, PickAsk) && talk.Stock == null) { talk.Kind = PlayerTalkKind.AskPick; }
        else if (talk.Stock != null && Has(lower, SellAsk)) { talk.Kind = PlayerTalkKind.AskSell; }
        else if (talk.Stock != null && Has(lower, BuyAsk)) { talk.Kind = PlayerTalkKind.AskBuy; }
        else if (talk.Stock != null && (Has(lower, OpinionAsk) || talk.Asks)) { talk.Kind = PlayerTalkKind.AskOpinion; }
        else if (talk.Stock != null && Has(lower, DownWords)) { talk.Kind = PlayerTalkKind.ClaimDown; }
        else if (talk.Stock != null && Has(lower, UpWords)) { talk.Kind = PlayerTalkKind.ClaimUp; }
        else if (Has(lower, Thank)) { talk.Kind = PlayerTalkKind.Thanks; }
        else if (Has(lower, Sorry)) { talk.Kind = PlayerTalkKind.Apology; }
        else if (Has(lower + " ", Greet)) { talk.Kind = PlayerTalkKind.Greeting; }
        else if (Has(lower, Farewell)) { talk.Kind = PlayerTalkKind.Bye; }
        else if (talk.Stock != null) { talk.Kind = PlayerTalkKind.AskOpinion; }
        else if (talk.Cries) { talk.Kind = PlayerTalkKind.Sad; }
        else if (talk.Laughs) { talk.Kind = PlayerTalkKind.Laugh; }
        else if (talk.CallsAll) { talk.Kind = PlayerTalkKind.CallAll; }
        return talk;
    }
    private static int Overlap(string words, Stock stock)
    {
        string text = Squeeze(words); int best = 0;
        foreach (string raw in new[] { GameLocale.Text(stock.stockName), stock.stockName, stock.symbol })
        {
            string name = Squeeze(raw);
            for (int size = Math.Min(name.Length, 12); size >= 2; size--) { if (text.Contains(name.Substring(0, size))) { best = Math.Max(best, size); break; } }
        }
        return best;
    }
}

[Serializable] public sealed class PlayerTalkGroup { public string key; public string[] texts; }
[Serializable] public sealed class PlayerTalkLocale { public int schemaVersion = 1; public PlayerTalkGroup[] groups; [NonSerialized] public Dictionary<string, string[]> index; }

/// <summary>플레이어의 말에 봇이 답하는 대사. 언어마다 따로 쓴 파일(Chat/player_talk.ko/en/ja)에서 온다.</summary>
public static class PlayerTalkAssets
{
    private static readonly string[] None = Array.Empty<string>();
    private static Dictionary<string, PlayerTalkLocale> locales;
    private static bool failed;
    public static bool Ready
    {
        get
        {
            if (locales == null && !failed)
            {
                var loaded = new Dictionary<string, PlayerTalkLocale>(StringComparer.Ordinal);
                foreach (string code in ChatHypeAssets.Languages)
                {
                    var asset = Resources.Load<TextAsset>("Chat/player_talk." + code);
                    var locale = asset == null ? null : JsonUtility.FromJson<PlayerTalkLocale>(asset.text);
                    if (locale == null || locale.groups == null) { continue; }
                    locale.index = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    foreach (var group in locale.groups) { if (group != null && !string.IsNullOrEmpty(group.key) && group.texts != null && group.texts.Length > 0) { locale.index[group.key] = group.texts; } }
                    loaded[code] = locale;
                }
                if (loaded.Count == 0) { failed = true; Debug.LogWarning("플레이어 채팅 답글을 끕니다: player_talk 파일이 없습니다."); } else { locales = loaded; }
            }
            return locales != null;
        }
    }
    /// <summary>"ask.bull.hostile"이 없으면 "ask.bull", 그것도 없으면 "ask"를 쓴다.</summary>
    public static string[] Lines(string key)
    {
        if (!Ready) { return None; }
        var locale = locales.TryGetValue(GameLocale.Code, out var current) ? current : locales.TryGetValue("ko", out current) ? current : null;
        if (locale == null) { foreach (var any in locales.Values) { locale = any; break; } }
        while (locale != null && key.Length > 0)
        {
            if (locale.index.TryGetValue(key, out var texts)) { return texts; }
            int cut = key.LastIndexOf('.');
            if (cut < 0) { break; }
            key = key.Substring(0, cut);
        }
        return None;
    }
}

public sealed partial class InvestorSociety
{
    public const string AnalystName = "종목 분석 AI";
    private const string PlayerTemplate = "PLAYER.CHAT";
    /// <summary>플레이어의 "오른다·내린다"를 이만큼(초) 뒤에 따진다. 그 사이 2% 넘게 그 방향으로 갔으면 맞은 것, 반대로 2% 넘게 갔으면 틀린 것.</summary>
    private const float CallMinSec = 90f, CallMaxSec = 150f; private const decimal CallMove = .02m;
    private sealed class TalkReply { public double due; public int room; public string key; public Stock stock; public long replyTo; public int onlyBot; public float percent; }
    private sealed class PlayerCall { public Stock stock; public decimal price; public bool up; public double due; public int room; public long messageId; }
    private sealed class AnalysisJob { public double due; public int room; public Stock stock; public string argument; public long replyTo; }
    private readonly List<TalkReply> talkReplies = new();
    private readonly List<PlayerCall> playerCalls = new();
    private readonly List<AnalysisJob> analysisJobs = new();
    private readonly Queue<(string text, double at)> playerRecent = new();
    private double talkNextReply, analysisNextRequest = -1, analysisBotRest;
    /// <summary>확인용: 최근에 읽은 뜻과 한 답.</summary>
    public readonly Queue<string> PlayerTalkTrace = new(64);
    public int PlayerLines { get; private set; }
    public int PlayerReplies { get; private set; }
    public int AnalysesPosted { get; private set; }
    private void TraceTalk(string line) { PlayerTalkTrace.Enqueue(line); while (PlayerTalkTrace.Count > 60) { PlayerTalkTrace.Dequeue(); } }
    private static string TalkWord(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    /// <summary>플레이어가 이 방에 글을 쓸 수 있는가: 공개방, 들어가 있는 새벽 관찰실, 열어 둔 내 리딩방.</summary>
    public bool PlayerCanWrite(int room)
    {
        if (day == null || !SocialReady) { return false; }
        if (room == 0) { return true; }
        if (room == NpcLeadingRoom.RoomId) { return NpcLeadingRoom.Instance != null && NpcLeadingRoom.Instance.PlayerJoined; }
        return IsRoomOpen(room);
    }

    private float chatBarCheck;
    /// <summary>채팅 화면에 글 쓰는 칸과 새벽 관찰실 잠금 화면을 붙인다(화면이 만들어진 뒤 한 번).</summary>
    private void EnsureChatBar()
    {
        if (Time.unscaledTime < chatBarCheck) { return; }
        chatBarCheck = Time.unscaledTime + 2f;
        foreach (var chat in FindObjectsByType<TabletGroupChatUI>(FindObjectsInactive.Include))
        { if (chat.scroll != null && chat.GetComponent<PlayerChatBar>() == null) { chat.gameObject.AddComponent<PlayerChatBar>(); } }
    }

    // ── 올리기 ──
    private long PostPlayer(int room, string text, ChatProfitCard card)
    {
        var social = new SocialMessage { id = ChatSocialIds.Next(), senderId = 0, roomId = room, at = SocialTime.GameMinute, realAt = SocialTime.Seconds, act = SocialAct.REACT, templateKey = PlayerTemplate,
            text = text, facts = new SocialFacts(), profitCard = card, debug = new SocialMessageDebug { trigger = "PLAYER", weights = Array.Empty<SocialWeight>(), slots = new SocialFacts() } };
        if (room == 0) { return day.PublishPlayerMessage(text, PlayerTemplate, social); }
        var target = GetRoom(room);
        target?.Add(new BotChatMessage(target.Version + 1, null, day.Day, day.GameMinute, text, PlayerTemplate, ChatEvent.Agreement, new InvestorMessageContext(0, room, 0, 1, 0, false), social, fromPlayer: true));
        return social.id;
    }
    private long PostAnalyst(int room, string text, ChatAnalysisCard card, long replyTo)
    {
        var social = new SocialMessage { id = ChatSocialIds.Next(), senderId = 0, roomId = room, replyToId = replyTo, at = SocialTime.GameMinute, realAt = SocialTime.Seconds, act = SocialAct.REACT,
            templateKey = TabletGroupChatUI.AnalysisTemplate, text = text, facts = new SocialFacts { symbol = card?.symbol, stockName = card?.stockName }, analysisCard = card,
            debug = new SocialMessageDebug { trigger = "ANALYST", weights = Array.Empty<SocialWeight>(), slots = new SocialFacts() } };
        if (room == 0) { return day.PublishSpeaker(TalkWord(AnalystName, "Stock Analysis AI", "銘柄分析AI"), text, TabletGroupChatUI.AnalysisTemplate, social); }
        var target = GetRoom(room);
        target?.Add(new BotChatMessage(target.Version + 1, null, day.Day, day.GameMinute, text, TabletGroupChatUI.AnalysisTemplate, ChatEvent.Agreement, new InvestorMessageContext(0, room, 0, 1, 0, false), social,
            speaker: TalkWord(AnalystName, "Stock Analysis AI", "銘柄分析AI")));
        return social.id;
    }
    /// <summary>봇의 한 줄을 그 방에 올린다. 표시만 한다(방의 대화 기억·난수열에 넣지 않는다).</summary>
    private long PostBotLine(TradeBot bot, int room, string text, string key, Stock stock, long replyTo)
    {
        var message = new SocialMessage { id = ++hypeMessageId, roomId = room, senderId = bot.Social.Psychology.SocialId, at = SocialTime.GameMinute, realAt = SocialTime.Seconds,
            act = SocialAct.REACT, replyToId = replyTo, topicEvent = "PLAYER_TALK", templateKey = key, text = text,
            facts = new SocialFacts { symbol = stock?.symbol, stockName = stock?.stockName, at = SocialTime.GameMinute }, debug = new SocialMessageDebug { trigger = "PLAYER_TALK" } };
        bool was = breathDelivering; breathDelivering = true;
        try { DeliverSocialMessage(message); } finally { breathDelivering = was; }
        return message.id;
    }

    // ── 플레이어가 쓴 말 ──
    /// <summary>플레이어가 채팅에 한 줄을 썼다. 쓸 수 없는 방이면 false.</summary>
    public bool SubmitPlayerChat(int room, string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0 || !PlayerCanWrite(room)) { return false; }
        if (text.Length > 80) { text = text.Substring(0, 80); }
        double now = SocialTime.Seconds;
        var talk = PlayerTalk.Read(text);
        var account = StockManager.Instance != null ? StockManager.Instance.playerAccount : null;
        // 같은 말을 20초 안에 또 쓰면 도배다. 글은 올라가지만 봇들은 한 번 핀잔을 주고 무시한다.
        bool spam = false; foreach (var recent in playerRecent) { if (recent.text == text && now - recent.at < 20) { spam = true; break; } }
        playerRecent.Enqueue((text, now)); while (playerRecent.Count > 6) { playerRecent.Dequeue(); }

        // 수익 자랑·손실 한탄은 내 실제 계좌와 맞춰 본다. 맞으면 봇들과 같은 인증 카드가 붙는다.
        ChatProfitCard card = null; decimal actual = 0m; bool held = false;
        if (talk.Kind == PlayerTalkKind.Brag || talk.Kind == PlayerTalkKind.Lament)
        {
            var subject = talk.Stock ?? BestHolding(account, talk.Kind == PlayerTalkKind.Brag);
            if (subject != null && account != null && account.GetHolding(subject) >= 1m && account.GetAvgPrice(subject) > 0m)
            {
                held = true; talk.Stock = subject;
                decimal average = account.GetAvgPrice(subject), quantity = account.GetHolding(subject);
                actual = (subject.currentPrice / average - 1m) * 100m;
                if (talk.Kind == PlayerTalkKind.Brag ? actual > 0m : actual < 0m)
                {
                    card = new ChatProfitCard { kind = ChatProfitCardKind.Holding, symbol = subject.symbol, stockName = subject.stockName, amount = (subject.currentPrice - average) * quantity,
                        percent = actual, averageCost = average, price = subject.currentPrice, quantity = quantity, capturedAt = SocialTime.GameMinute };
                }
            }
        }
        long id = PostPlayer(room, text, card);
        PlayerLines++;
        TraceTalk("나: " + text + "  → " + talk.Kind + (talk.Stock != null ? " " + talk.Stock.symbol : "") + (float.IsNaN(talk.Percent) ? "" : " " + talk.Percent.ToString("0.#") + "%") + (card != null ? " 인증 " + actual.ToString("+0.0;-0.0") + "%" : "")
            + (talk.CallsAll ? " 부름" : "") + (talk.Laughs ? " 웃음" : "") + (spam ? " 도배" : ""));
        if (spam) { Plan(room, "spam", null, id, now + 1.2, 0, 1, .7f); return true; }

        switch (talk.Kind)
        {
            case PlayerTalkKind.Analyze:
                if (room != NpcLeadingRoom.RoomId) { Plan(room, "aiOnly", null, id, now + 1.4, 0, 1, .9f); break; }
                analysisJobs.Add(new AnalysisJob { due = now + 1.2 + HypeRandom.Next(), room = room, stock = talk.Stock, argument = talk.Argument, replyTo = id });
                break;
            case PlayerTalkKind.Insult:
                Hearers(room, (bot, mind) => mind.ReactToPlayerTalk(-2f, HypeRandom.Next() < .5 ? 6f + mind.Traits.Grudge / 100f * 10f : 2f, "플레이어의 막말"));
                Plan(room, "insult", talk.Stock, id, now + 1.0, 0, HypeRandom.Range(1, 4), 1f);
                break;
            case PlayerTalkKind.Brag:
                if (card != null)
                {
                    float gain = (float)actual;
                    // 인증된 수익: 클수록 믿음이 오른다. 그 종목에 물려 있거나 원한이 많은 봇은 배가 아프다.
                    Hearers(room, (bot, mind) =>
                    {
                        bool sore = bot.GetOwnedVolume(talk.Stock) > 0m && bot.GetAvgPrice(talk.Stock) > talk.Stock.currentPrice || mind.Traits.Grudge >= 70;
                        mind.ReactToPlayerTalk(Mathf.Clamp(gain * 1f, 2f, 25f) * (sore ? .3f : 1f), sore ? Mathf.Clamp(gain * .5f, 2f, 16f) : 0f, "플레이어의 수익 인증 " + gain.ToString("+0.0") + "%");
                    });
                    Plan(room, "brag.proof.praise", talk.Stock, id, now + 1.2, 0, HypeRandom.Range(1, 3), 1f, gain);
                    Plan(room, "brag.proof.envy", talk.Stock, id, now + 2.6, 0, gain >= 10f ? HypeRandom.Range(1, 3) : HypeRandom.Range(0, 2), .8f, gain);
                    Sway(room, talk.Stock, 1.2f);
                }
                else if (held)
                {
                    // 들고는 있는데 손실 중이다. 자랑이 거짓이다.
                    Hearers(room, (bot, mind) => mind.ReactToPlayerTalk(-5f, 1f, "플레이어의 거짓 자랑"));
                    Plan(room, "brag.loss", talk.Stock, id, now + 1.4, 0, HypeRandom.Range(1, 3), 1f, (float)actual);
                }
                else
                {
                    Hearers(room, (bot, mind) => mind.ReactToPlayerTalk(-3f, 0f, "플레이어의 인증 없는 자랑"));
                    Plan(room, "brag.noproof", talk.Stock, id, now + 1.3, 0, HypeRandom.Range(1, 3), 1f);
                }
                break;
            case PlayerTalkKind.Lament:
                Hearers(room, (bot, mind) => mind.ReactToPlayerTalk(card != null ? 2f : .5f, 0f, "플레이어의 손실 한탄"));
                Plan(room, "lament.comfort", talk.Stock, id, now + 1.3, 0, HypeRandom.Range(1, 3), .9f, (float)actual);
                Plan(room, "lament.mock", talk.Stock, id, now + 2.8, 0, HypeRandom.Range(0, 2), .7f, (float)actual);
                break;
            case PlayerTalkKind.AskBuy: case PlayerTalkKind.AskSell: case PlayerTalkKind.AskOpinion:
                Plan(room, talk.Kind == PlayerTalkKind.AskBuy ? "askBuy" : talk.Kind == PlayerTalkKind.AskSell ? "askSell" : "ask", talk.Stock, id, now + 1.3, 0, HypeRandom.Range(1, 4), .9f);
                Sway(room, talk.Stock, .4f);
                break;
            case PlayerTalkKind.AskPick:
                Plan(room, "askPick", null, id, now + 1.4, 0, HypeRandom.Range(1, 4), .9f);
                break;
            case PlayerTalkKind.ClaimUp: case PlayerTalkKind.ClaimDown:
                bool up = talk.Kind == PlayerTalkKind.ClaimUp;
                Plan(room, up ? "claimUp" : "claimDown", talk.Stock, id, now + 1.2, 0, HypeRandom.Range(1, 4), .9f);
                if (playerCalls.Count < 3) { playerCalls.Add(new PlayerCall { stock = talk.Stock, price = talk.Stock.currentPrice, up = up, due = now + CallMinSec + HypeRandom.Next() * (CallMaxSec - CallMinSec), room = room, messageId = id }); }
                if (up) { Sway(room, talk.Stock, 1f); }
                break;
            case PlayerTalkKind.Whale:
                Plan(room, "whale", talk.Stock, id, now + 1.2, 0, HypeRandom.Range(1, 4), .95f);
                if (talk.Stock != null) { Sway(room, talk.Stock, .8f); }
                break;
            case PlayerTalkKind.Thanks:
                Plan(room, "thanks", null, id, now + 1.2, 0, 1, .55f);
                break;
            case PlayerTalkKind.Apology:
                Hearers(room, (bot, mind) => mind.ReactToPlayerTalk(0f, -4f, "플레이어의 사과"));
                Plan(room, "apology", null, id, now + 1.2, 0, 1, .7f);
                break;
            case PlayerTalkKind.Greeting:
                Plan(room, "greet", null, id, now + 1.0, 0, HypeRandom.Range(1, 3), .75f);
                break;
            case PlayerTalkKind.Bye:
                Plan(room, "bye", null, id, now + 1.0, 0, HypeRandom.Range(1, 3), .6f);
                break;
            case PlayerTalkKind.Sad:
                Plan(room, "sad", null, id, now + 1.3, 0, HypeRandom.Range(1, 3), .6f);
                break;
            case PlayerTalkKind.Laugh:
                Plan(room, "laugh", null, id, now + 1.0, 0, HypeRandom.Range(1, 3), .5f);
                break;
            case PlayerTalkKind.CallAll:
                // 아무나 부르는 말에는 대답이 드물다.
                Plan(room, "all", null, id, now + 1.4, 0, HypeRandom.Next() < .25 ? 2 : 1, .4f);
                break;
            default:
                Plan(room, "other", null, id, now + 1.6, 0, 1, .18f);
                break;
        }
        // 웃음·울음이 섞인 말에는 기분에 따라 한마디 더 붙기도 한다.
        if (talk.Kind != PlayerTalkKind.Laugh && talk.Laughs) { Plan(room, "laugh", null, id, now + 2.4, 0, 1, .25f); }
        if (talk.Kind != PlayerTalkKind.Sad && talk.Kind != PlayerTalkKind.Lament && talk.Cries) { Plan(room, "sad", null, id, now + 2.4, 0, 1, .25f); }
        return true;
    }
    private static Stock BestHolding(Trader account, bool winner)
    {
        if (account == null || StockManager.Instance == null) { return null; }
        Stock best = null; decimal bestReturn = 0m;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || account.GetHolding(stock) < 1m || account.GetAvgPrice(stock) <= 0m) { continue; }
            decimal result = stock.currentPrice / account.GetAvgPrice(stock) - 1m;
            if (best == null || (winner ? result > bestReturn : result < bestReturn)) { best = stock; bestReturn = result; }
        }
        return best;
    }
    /// <summary>답글을 예약한다. count줄까지, 줄마다 chance의 확률로.</summary>
    private void Plan(int room, string key, Stock stock, long replyTo, double first, int onlyBot, int count, float chance, float percent = float.NaN)
    {
        for (int i = 0; i < count; i++)
        {
            if (HypeRandom.Next() >= chance) { continue; }
            talkReplies.Add(new TalkReply { due = first + i * (1.1 + HypeRandom.Next() * 1.6), room = room, key = key, stock = stock, replyTo = replyTo, onlyBot = onlyBot, percent = percent });
        }
        while (talkReplies.Count > 12) { talkReplies.RemoveAt(0); }
    }
    /// <summary>그 방에서 지금 채팅을 보고 있는 봇들.</summary>
    private void Hearers(int room, Action<TradeBot, InvestorPsychology> act)
    {
        float now = SessionClock.Time;
        foreach (var bot in socialBots)
        {
            if (bot.Social == null || !bot.isActiveAndEnabled || !Present(bot, room) || !bot.Social.Psychology.ActiveAt(now)) { continue; }
            act(bot, bot.Social.Psychology);
        }
    }
    /// <summary>내 말이 눈길을 끈다. 나를 믿는 봇일수록, 잘 휩쓸리는 봇일수록 내가 말한 종목을 들여다본다.</summary>
    private void Sway(int room, Stock stock, float weight)
    {
        if (stock == null || !stock.AcceptsOrders) { return; }
        float now = SessionClock.Time; int turned = 0;
        Hearers(room, (bot, mind) =>
        {
            float chance = (.04f + mind.Trust / 100f * .5f) * (.3f + mind.Traits.Suggestibility / 100f * .7f) * weight;
            if (mind.HasPanic || UnityEngine.Random.value >= chance) { return; }
            bot.Social.InfluencedStock = stock; bot.Social.InfluenceUntil = now + 30f + mind.Trust * .5f; turned++;
        });
        if (turned > 0) { TraceTalk("    (내 말에 눈 돌린 봇 " + turned + ", " + stock.symbol + ")"); }
    }

    // ── 봇의 답 ──
    private void TickPlayerChat()
    {
        if (day == null || !SocialReady) { return; }
        double now = SocialTime.Seconds;
        for (int i = 0; i < analysisJobs.Count;)
        {
            if (analysisJobs[i].due > now) { i++; continue; }
            var job = analysisJobs[i]; analysisJobs.RemoveAt(i); PostAnalysis(job, now);
        }
        if (now >= talkNextReply)
        {
            for (int i = 0; i < talkReplies.Count; i++)
            {
                if (talkReplies[i].due > now) { continue; }
                var reply = talkReplies[i]; talkReplies.RemoveAt(i);
                if (now - reply.due < 20 && SpeakReply(reply, now)) { talkNextReply = now + .9 + HypeRandom.Next() * .8; }
                break;
            }
        }
        ResolvePlayerCalls(now);
        TickAnalysisRequests(now);
    }
    private bool SpeakReply(TalkReply reply, double now)
    {
        if (!PlayerTalkAssets.Ready) { return false; }
        // 그 방에서 지금 말할 수 있는 봇 가운데 하나. 존댓말 봇은 이런 수다에 끼지 않는다.
        hypeSpeakers.Clear(); float clock = SessionClock.Time;
        foreach (var candidate in socialBots)
        {
            if (candidate.Social == null || !candidate.isActiveAndEnabled || !Present(candidate, reply.room) || !candidate.Social.Psychology.ActiveAt(clock) || IsTyping(candidate) || candidate == ResolveOddball() ||
                candidate.Social.Psychology.Traits.Politeness >= Config.Int("styleFormalMin") || hypeSpokeAt.TryGetValue(candidate.Social.Id, out double spoke) && now - spoke < 6)
            { continue; }
            hypeSpeakers.Add(candidate);
        }
        if (hypeSpeakers.Count == 0) { return false; }
        var bot = hypeSpeakers[HypeRandom.Range(0, hypeSpeakers.Count)]; var mind = bot.Social.Psychology;
        // 이 봇의 마음: 나를 어떻게 보는가, 그 종목을 어떻게 보는가, 지금 기분이 어떤가.
        string stance = mind.Quadrant == InvestorQuadrant.Hostile || mind.Betrayal >= 40f ? "hostile" : mind.Trust >= 40f ? "friendly" : null;
        string mood = mind.Fear >= 55f ? "scared" : mind.Greed >= 60f ? "happy" : "dry";
        string key = reply.key; Stock stock = reply.stock; Stock pick = null;
        if (stock != null && (key == "ask" || key == "askBuy" || key == "askSell" || key == "claimUp" || key == "claimDown"))
        {
            decimal believed = bot.BelievedPrice(stock), price = stock.currentPrice;
            bool bull = believed > price * 1.03m, bear = believed < price * .97m;
            key += key == "claimUp" ? (bear ? ".doubt" : ".agree") : key == "claimDown" ? (bull ? ".doubt" : ".agree") : bull ? ".bull" : bear ? ".bear" : ".neutral";
        }
        else if (key == "laugh" || key == "sad") { key += "." + mood; }
        else if (key == "whale") { key += mind.Traits.Suggestibility >= 55 ? ".excited" : ".doubt"; }
        else if (key == "askPick")
        {
            // 이 봇이 지금 가장 싸다고 믿는 종목을 댄다. 없으면 모르겠다고 한다.
            decimal best = 1.03m;
            foreach (var candidate in StockManager.Instance.stocks)
            {
                if (candidate == null || !candidate.AcceptsOrders || candidate.currentPrice <= 0m) { continue; }
                decimal edge = bot.BelievedPrice(candidate) / candidate.currentPrice;
                if (edge > best) { best = edge; pick = candidate; }
            }
            if (pick == null) { key = "askPick.none"; }
        }
        if (stance != null) { key += "." + stance; }
        string text = PickHypeText(PlayerTalkAssets.Lines(key), Array.Empty<string>());
        if (text == null) { return false; }
        var subject = stock ?? pick;
        var slots = subject != null ? ChartSlots(subject, subject.currentPrice, subject.currentPrice, 0m) : HypeSlots(null, 0, 0m);
        if (pick != null) { slots["pick"] = GameLocale.Text(pick.stockName ?? pick.symbol ?? ""); }
        slots["ret"] = float.IsNaN(reply.percent) ? "" : Mathf.Abs(reply.percent).ToString("0");
        string line = ChatHypeAssets.Fill(text, slots);
        PostBotLine(bot, reply.room, line, "talk." + key, subject, reply.replyTo);
        hypeSpokeAt[bot.Social.Id] = now; PlayerReplies++;
        // 말을 섞은 봇은 나와 조금 가까워진다(시비와 조롱은 빼고).
        if (!key.StartsWith("insult", StringComparison.Ordinal) && !key.Contains("mock") && !key.Contains("hostile")) { mind.ReactToPlayerTalk(1.5f, 0f, "플레이어와 대화"); }
        TraceTalk("    > " + bot.Social.Nickname + ": " + line + "  [" + key + "]");
        return true;
    }
    private void ResolvePlayerCalls(double now)
    {
        for (int i = playerCalls.Count - 1; i >= 0; i--)
        {
            var call = playerCalls[i];
            if (now < call.due) { continue; }
            playerCalls.RemoveAt(i);
            if (!call.stock.AcceptsOrders || call.price <= 0m) { continue; }
            decimal move = call.stock.currentPrice / call.price - 1m;
            bool right = call.up ? move >= CallMove : move <= -CallMove, wrong = call.up ? move <= -CallMove : move >= CallMove;
            if (!right && !wrong) { continue; }
            // 맞으면 믿음이, 틀리면 원망이 쌓인다. 그 말을 들었던 방의 봇들에게.
            Hearers(call.room, (bot, mind) => mind.ReactToPlayerTalk(right ? 5f + mind.Credulity * 6f : -2f, right ? 0f : 3f + mind.Traits.Grudge / 100f * 5f, right ? "플레이어의 예측 적중" : "플레이어의 예측 빗나감"));
            TraceTalk("    (예측 " + (right ? "적중" : "빗나감") + ": " + call.stock.symbol + " " + (call.up ? "오른다" : "내린다") + ", " + (move * 100m).ToString("+0.0;-0.0") + "%)");
            Plan(call.room, right ? "callRight" : "callWrong", call.stock, call.messageId, now + .5, 0, HypeRandom.Range(1, 3), .9f, (float)(move * 100m));
        }
    }

    // ── 종목 분석 AI ──
    /// <summary>
    /// 매수 추천 점수(0~100). 공개된 것만 본다: 최근 1분의 움직임, 당일 고가·저가 사이의 자리, 호가창의 벽, 거래가 평소보다 붙었는지.
    /// 오르는 중이고 받치는 벽이 있으면 높고, 고가 근처에서 힘이 빠지거나 매도벽에 눌리면 낮다.
    /// </summary>
    public ChatAnalysisCard Analyze(Stock stock, out string reason)
    {
        reason = "";
        if (stock == null || stock.currentPrice <= 0m) { return null; }
        decimal price = stock.currentPrice, open = price, high = price, low = price;
        if (StockStatistics.Instance != null && StockStatistics.Instance.TryGetDailyCandle(stock, out var candle) && candle.open > 0f)
        { open = (decimal)candle.open; high = Math.Max(price, (decimal)candle.high); low = Math.Min(price, (decimal)candle.low); }
        float momentum = 0f;
        if (chartTracks.TryGetValue(stock, out var track) && PriceAgo(track, SocialTime.Seconds, 60, out decimal before) && before > 0m) { momentum = (float)(price / before) - 1f; }
        float position = high > low ? (float)((price - low) / (high - low)) : .5f;
        float lean = ManipulationTools.Walls(stock).Lean;
        float heat = StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock);
        float score = 50f + 22f * (float)Math.Tanh(momentum * 25f) + 10f * lean;
        if (heat >= 3f) { score += momentum > 0f ? 8f : momentum < 0f ? -8f : 0f; }
        if (position > .9f) { score -= (position - .9f) * 140f * (momentum <= 0f ? 1f : .3f); }
        if (position < .15f) { score += (.15f - position) / .15f * 12f * (momentum >= 0f ? 1f : -.3f); }
        int final = Mathf.Clamp(Mathf.RoundToInt(score), 3, 97);
        // 가장 크게 작용한 까닭 하나를 말로.
        string move = (momentum * 100f).ToString("+0.0;-0.0");
        reason = Mathf.Abs(momentum) >= .01f
            ? (momentum > 0f ? TalkWord("최근 1분 " + move + "%로 오르는 중입니다.", "Up " + move + "% over the last minute.", "直近1分で" + move + "%上昇中です。")
                : TalkWord("최근 1분 " + move + "%로 밀리는 중입니다.", "Down " + move + "% over the last minute.", "直近1分で" + move + "%下落中です。"))
            : lean >= .4f ? TalkWord("아래에 큰 매수 잔량이 받치고 있습니다.", "A large bid is sitting underneath.", "下に大きな買い板があります。")
            : lean <= -.4f ? TalkWord("위에 큰 매도 잔량이 막고 있습니다.", "A large offer is capping it overhead.", "上に大きな売り板があります。")
            : position > .9f ? TalkWord("당일 고가 부근입니다.", "Trading near the day's high.", "本日の高値付近です。")
            : position < .15f ? TalkWord("당일 저가 부근입니다.", "Trading near the day's low.", "本日の安値付近です。")
            : TalkWord("뚜렷한 방향이 없습니다.", "No clear direction.", "はっきりした方向感はありません。");
        if (heat >= 3f) { reason += " " + TalkWord("거래가 평소보다 크게 붙었습니다.", "Volume is well above normal.", "出来高が普段よりかなり多いです。"); }
        return new ChatAnalysisCard { symbol = stock.symbol, stockName = stock.stockName, open = open, price = price, high = high, low = low, score = final, capturedAt = SocialTime.GameMinute };
    }
    private void PostAnalysis(AnalysisJob job, double now)
    {
        if (job.stock == null)
        {
            PostAnalyst(job.room, string.IsNullOrEmpty(job.argument)
                ? TalkWord("종목 이름을 함께 적어 주세요. 예: !분석 노보", "Add a stock name. Example: !analyze Nobo", "銘柄名を付けてください。例: !分析 ノボ")
                : TalkWord("'" + job.argument + "'에 맞는 종목을 찾지 못했습니다.", "No stock matches '" + job.argument + "'.", "「" + job.argument + "」に合う銘柄が見つかりません。"), null, job.replyTo);
            return;
        }
        var card = Analyze(job.stock, out string reason);
        if (card == null) { return; }
        string name = GameLocale.Text(job.stock.stockName ?? job.stock.symbol);
        string verdict = TabletGroupChatUI.AnalysisVerdict(card.score);
        long id = PostAnalyst(job.room, TalkWord(name + " 분석입니다. " + reason + " 지금은 '" + verdict + "' 구간으로 봅니다. 참고용입니다.",
            name + ". " + reason + " I read this as '" + verdict + "' for now. For reference only.", name + "の分析です。" + reason + " 現時点では「" + verdict + "」と見ます。参考情報です。"), card, job.replyTo);
        AnalysesPosted++;
        TraceTalk("    > AI: " + job.stock.symbol + " 점수 " + card.score + " (" + verdict + ") 시가 " + Money(card.open) + " 현재 " + Money(card.price) + " 고가 " + Money(card.high) + " 저가 " + Money(card.low) + " · " + reason);
        // 가끔 봇 하나가 결과에 한마디 한다. 그 말대로 사고팔지는 않는다.
        Plan(job.room, card.score >= 58 ? "aiReact.buy" : card.score < 42 ? "aiReact.sell" : "aiReact.hold", job.stock, id, now + 1.6, 0, 1, .55f);
    }
    /// <summary>새벽 관찰실의 봇들도 가끔 AI에게 분석을 시킨다.</summary>
    private void TickAnalysisRequests(double now)
    {
        if (day.Phase != MarketPhase.Trading || StockManager.Instance == null) { return; }
        if (analysisNextRequest < 0) { analysisNextRequest = now + 20 + HypeRandom.Next() * 30; }
        if (now < analysisNextRequest) { return; }
        analysisNextRequest = now + 55 + HypeRandom.Next() * 70;
        const int room = NpcLeadingRoom.RoomId;
        hypeSpeakers.Clear(); float clock = SessionClock.Time;
        foreach (var candidate in socialBots)
        { if (candidate.Social != null && candidate.isActiveAndEnabled && Present(candidate, room) && candidate.Social.Psychology.ActiveAt(clock) && !IsTyping(candidate)) { hypeSpeakers.Add(candidate); } }
        if (hypeSpeakers.Count == 0) { return; }
        var bot = hypeSpeakers[HypeRandom.Range(0, hypeSpeakers.Count)];
        // 눈길이 가 있는 종목(누가 말해서 보게 된 종목), 들고 있는 종목, 달아오른 종목 순으로 고른다.
        Stock stock = bot.Social.InfluencedStock != null && SessionClock.Time < bot.Social.InfluenceUntil && bot.Social.InfluencedStock.AcceptsOrders ? bot.Social.InfluencedStock : null; float hottest = 0f;
        if (stock == null)
        {
            foreach (var candidate in StockManager.Instance.stocks)
            {
                if (candidate == null || !candidate.AcceptsOrders) { continue; }
                float weight = (bot.GetOwnedVolume(candidate) > 0m ? 2f : 0f) + (StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(candidate)) + (float)HypeRandom.Next() * 2f;
                if (stock == null || weight > hottest) { hottest = weight; stock = candidate; }
            }
        }
        if (stock == null) { return; }
        string name = GameLocale.Text(stock.stockName ?? stock.symbol);
        // 봇도 이름을 줄여 쓰곤 한다.
        if (name.Length >= 4 && HypeRandom.Next() < .35) { name = name.Substring(0, Math.Max(2, name.Length / 2)); }
        long asked = PostBotLine(bot, room, TalkWord("!분석 ", "!analyze ", "!分析 ") + name, "talk.analyze", stock, 0);
        hypeSpokeAt[bot.Social.Id] = now;
        analysisJobs.Add(new AnalysisJob { due = now + 1.4 + HypeRandom.Next(), room = room, stock = stock, argument = name, replyTo = asked });
    }
}
