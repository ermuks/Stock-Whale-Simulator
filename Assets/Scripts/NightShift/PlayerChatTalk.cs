using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어와 봇이 말을 주고받는 흐름.
//
// 1) 봇이 던진 물음을 기억한다. 방에 대고 물은 말("다들 뭐 보고 있음?")과 내게 되물은 말이 대상이다(다른 봇에게 단 답글, 급등락 때 쏟아지는 말은 뺀다).
//    그 뒤 75초 안에 내가 그 방에 답하면 물은 봇이 내 답을 받는다: 종목을 댔으면 그 종목을 자기 생각대로 받고, "ㅇㅇ"·"ㄴㄴ"·"몰라"에도 맞게 받는다.
// 2) 방금 나와 말을 섞은 봇을 기억한다. 45초 안에 내가 또 말하면 그 봇이 먼저 받는다. "왜?"·"진짜?"라고 되물으면 그 봇이 자기 말을 풀어 준다.
// 3) 묻는 말에는 봇이 보고 아는 것으로 답한다: 공개된 기사(NewsManager), 당일 등락과 달아오른 종목, 호가창의 벽, 자기 계좌의 보유와 평단.
//    진짜 가치나 세력의 속사정은 봇도 모른다. 모르면 모른다고 한다.
//
// 표시와 친밀감만 바꾼다. 주문을 내거나 봇의 믿는 값을 바꾸지 않는다.
public sealed partial class InvestorSociety
{
    /// <summary>봇의 물음이 열려 있는 시간, 말을 섞은 봇이 다음 말을 먼저 받는 시간(초).</summary>
    private const double QuestionOpenSec = 75, PartnerSec = 45;
    private sealed class BotQuestion { public int bot, room; public double at; public Stock stock; }
    private sealed class TalkPartner { public int bot, room; public double at; public Stock stock; }
    private readonly List<BotQuestion> botQuestions = new();
    private readonly List<TalkPartner> talkPartners = new();
    private readonly List<string> talkLines = new();
    private string talkSaid;
    /// <summary>확인용: 봇의 물음에 내가 답한 횟수.</summary>
    public int PlayerAnswersHeard { get; private set; }

    // ── 봇의 물음 ──
    /// <summary>봇이 올린 글이 물음이면 기억해 둔다. 모든 봇 글이 지나는 길(DeliverSocialMessage)에서 부른다.</summary>
    private void NoteBotQuestion(TradeBot bot, SocialMessage message)
    {
        if (bot == null || bot.Social == null || message == null || string.IsNullOrEmpty(message.text) || message.targetId != 0) { return; }
        bool toPlayer = message.topicEvent == "PLAYER_TALK";
        if (message.topicEvent == "HYPE" || !toPlayer && message.replyToId != 0 || message.templateKey == "talk.analyze") { return; }
        if (message.act != SocialAct.ASK && message.text.IndexOf('?') < 0 && message.text.IndexOf('？') < 0) { return; }
        double now = SocialTime.Seconds; int id = bot.Social.Id, room = message.roomId;
        for (int i = botQuestions.Count - 1; i >= 0; i--)
        { if (now - botQuestions[i].at > QuestionOpenSec || botQuestions[i].bot == id && botQuestions[i].room == room) { botQuestions.RemoveAt(i); } }
        botQuestions.Add(new BotQuestion { bot = id, room = room, at = now, stock = StockBySymbol(message.facts?.symbol) });
        while (botQuestions.Count > 8) { botQuestions.RemoveAt(0); }
    }
    private BotQuestion OpenQuestion(int room, double now)
    {
        for (int i = botQuestions.Count - 1; i >= 0; i--)
        {
            var question = botQuestions[i];
            if (question.room == room && now - question.at <= QuestionOpenSec && SocialBots.TryGetValue(question.bot, out var bot) && Present(bot, room)) { return question; }
        }
        return null;
    }
    /// <summary>
    /// 열려 있는 봇의 물음에 내가 답했는가. 답했으면 물은 봇이 받는다.
    /// 종목 얘기·맞장구·그냥 하는 말은 여기서 답글까지 정하고 true를 돌려준다. 잡담·웃음은 물은 봇이 먼저 받도록만 해 두고 평소대로 간다.
    /// </summary>
    private bool HearPlayerAnswer(int room, PlayerTalk talk, long id, double now, ref int near)
    {
        var open = OpenQuestion(room, now);
        if (open == null || talk.Asks) { return false; }
        bool direct = talk.Kind == PlayerTalkKind.Mention || talk.Kind == PlayerTalkKind.ClaimUp || talk.Kind == PlayerTalkKind.ClaimDown || talk.Kind == PlayerTalkKind.Agree || talk.Kind == PlayerTalkKind.Disagree ||
            talk.Kind == PlayerTalkKind.Dunno || talk.Kind == PlayerTalkKind.Statement || talk.Kind == PlayerTalkKind.Other;
        bool soft = talk.Kind == PlayerTalkKind.Topic || talk.Kind == PlayerTalkKind.Laugh || talk.Kind == PlayerTalkKind.Sad || talk.Kind == PlayerTalkKind.Surprise;
        if (!direct && !soft) { return false; }
        botQuestions.Remove(open); PlayerAnswersHeard++; near = open.bot;
        if (SocialBots.TryGetValue(open.bot, out var asker))
        {
            // 대답해 준 사람에게는 조금 마음이 간다.
            asker.Social.Psychology.ReactToPlayerTalk(2f, 0f, "플레이어가 물음에 답함");
            TraceTalk("    (" + asker.Social.Nickname + "의 물음에 답함)");
        }
        if (soft) { return false; }
        string key = talk.Stock != null ? "answered.stock" : talk.Kind == PlayerTalkKind.Agree ? "answered.yes" : talk.Kind == PlayerTalkKind.Disagree ? "answered.no" : talk.Kind == PlayerTalkKind.Dunno ? "answered.dunno" : "answered";
        Plan(room, key, talk.Stock, id, now + 1.1, open.bot, 1, 1f, sure: true);
        if (talk.Stock != null)
        {
            // 옆에서 듣던 봇이 그 종목 얘기를 거들기도 한다.
            Plan(room, "mention", talk.Stock, id, now + 2.7, 0, 1, .5f, notBot: open.bot);
            Sway(room, talk.Stock, .6f);
            if (talk.Kind != PlayerTalkKind.Mention) { NotePlayerCall(room, talk.Stock, talk.Kind == PlayerTalkKind.ClaimUp, id, now); }
        }
        return true;
    }

    // ── 말을 섞은 봇 ──
    private TalkPartner TalkPartnerIn(int room, double now)
    {
        foreach (var partner in talkPartners)
        { if (partner.room == room && now - partner.at <= PartnerSec && SocialBots.TryGetValue(partner.bot, out var bot) && Present(bot, room)) { return partner; } }
        return null;
    }
    private void NoteTalkPartner(int room, TradeBot bot, Stock stock, double now)
    {
        for (int i = talkPartners.Count - 1; i >= 0; i--) { if (talkPartners[i].room == room || now - talkPartners[i].at > PartnerSec) { talkPartners.RemoveAt(i); } }
        talkPartners.Add(new TalkPartner { bot = bot.Social.Id, room = room, at = now, stock = stock });
    }

    // ── 누가 말하나 ──
    /// <summary>
    /// 답할 봇을 고른다. 맡은 봇(물은 봇, 방금 말을 섞은 봇)이 방에 있으면 그 봇이다.
    /// 아니면 지금 채팅을 보고 있고 방금 말하지 않은 반말 봇 가운데 하나. 그런 봇이 없으면 자리를 비운 봇, 그래도 없으면 존댓말 봇까지 넓힌다(작은 방에서도 답이 나오게).
    /// </summary>
    private TradeBot PickTalkSpeaker(TalkReply reply, double now)
    {
        if (reply.onlyBot != 0 && SocialBots.TryGetValue(reply.onlyBot, out var wanted) && wanted != null && wanted.Social != null && Present(wanted, reply.room) && !IsTyping(wanted)) { return wanted; }
        float clock = SessionClock.Time; var odd = ResolveOddball(); int formal = Config.Int("styleFormalMin");
        for (int pass = 0; pass < 3; pass++)
        {
            hypeSpeakers.Clear();
            foreach (var candidate in socialBots)
            {
                if (candidate.Social == null || !candidate.isActiveAndEnabled || !Present(candidate, reply.room) || candidate == odd || candidate.Social.Id == reply.notBot || IsTyping(candidate)) { continue; }
                var mind = candidate.Social.Psychology;
                if (pass < 2 && mind.Traits.Politeness >= formal) { continue; }
                if (pass < 1 && (!mind.ActiveAt(clock) || hypeSpokeAt.TryGetValue(candidate.Social.Id, out double spoke) && now - spoke < 6)) { continue; }
                hypeSpeakers.Add(candidate);
            }
            if (hypeSpeakers.Count > 0) { return hypeSpeakers[HypeRandom.Range(0, hypeSpeakers.Count)]; }
        }
        return null;
    }
    /// <summary>묻는 말에 다는 답인가(존댓말 봇이 받을 때 말투를 고르는 데 쓴다).</summary>
    private static bool AsksKey(string key) => key == "question" || key == "all" || key == "follow" || key == "how" || key == "market" || key == "hot" || key == "whaleTalk" || key == "holdings" || key == "profit" || key == "why" ||
        key.StartsWith("ask", StringComparison.Ordinal) || key.StartsWith("news", StringComparison.Ordinal);
    /// <summary>나를 싫어하는 봇이 시큰둥하게 받아도 되는 가벼운 말인가.</summary>
    private static bool SoftKey(string key) => key == "question" || key == "statement" || key == "agree" || key == "disagree" || key == "dunno" || key == "surprise" || key == "how" || key.StartsWith("topic", StringComparison.Ordinal);
    /// <summary>채울 수 없는 빈칸이 든 줄은 뺀다(종목이 없는데 종목 이름을 부르는 줄 따위).</summary>
    private string[] UsableLines(string[] lines, bool hasStock, bool hasNews, bool hasEntry, bool hasSaid)
    {
        talkLines.Clear();
        foreach (string line in lines)
        {
            if (line.IndexOf('{') < 0) { talkLines.Add(line); continue; }
            if (!hasStock && (line.Contains("{stockName}") || line.Contains("{price}") || line.Contains("{dayPct}") || line.Contains("{movePct}"))) { continue; }
            if (!hasNews && line.Contains("{news}") || !hasSaid && line.Contains("{said}") || !hasEntry && (line.Contains("{entry}") || line.Contains("{gain}"))) { continue; }
            talkLines.Add(line);
        }
        return talkLines.ToArray();
    }

    // ── 봇이 보고 아는 것 ──
    private static Stock StockBySymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol) || StockManager.Instance == null) { return null; }
        foreach (var stock in StockManager.Instance.stocks) { if (stock != null && stock.symbol == symbol) { return stock; } }
        return null;
    }
    /// <summary>가장 최근에 나온 기사. 종목을 주면 그 종목의 기사만 본다.</summary>
    private static NewsArticle LatestNews(Stock stock)
    {
        var desk = NewsManager.Instance;
        if (desk == null) { return null; }
        foreach (var article in desk.Articles)
        {
            if (article == null || string.IsNullOrWhiteSpace(article.title)) { continue; }
            if (stock == null || article.symbol == stock.symbol) { return article; }
        }
        return null;
    }
    /// <summary>당일 등락률(%). 기준가를 모르면 0.</summary>
    private static decimal DayMove(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m || ReadMarketDay == null) { return 0m; }
        var board = ReadMarketDay(stock);
        return board.reference > 0m ? (stock.currentPrice / board.reference - 1m) * 100m : 0m;
    }
    /// <summary>당일 가장 크게 움직인 종목과, 오른 종목·내린 종목의 수.</summary>
    private static Stock TopMover(out decimal move, out int ups, out int downs)
    {
        move = 0m; ups = downs = 0; Stock top = null;
        if (StockManager.Instance == null) { return null; }
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || !stock.AcceptsOrders || stock.currentPrice <= 0m) { continue; }
            decimal moved = DayMove(stock);
            if (moved >= .5m) { ups++; } else if (moved <= -.5m) { downs++; }
            if (top == null || Math.Abs(moved) > Math.Abs(move)) { top = stock; move = moved; }
        }
        return top;
    }
    /// <summary>거래가 평소보다 가장 크게 붙은 종목.</summary>
    private static Stock HottestStock(out float heat)
    {
        heat = 0f; Stock hottest = null;
        if (StockManager.Instance == null || StockInterestManager.Instance == null) { return null; }
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || !stock.AcceptsOrders) { continue; }
            float value = StockInterestManager.Instance.Heat(stock);
            if (hottest == null || value > heat) { hottest = stock; heat = value; }
        }
        return hottest;
    }
    /// <summary>이 봇이 가장 많이(평가금 기준) 들고 있는 종목. 없으면 null.</summary>
    private static Stock BiggestHolding(TradeBot bot)
    {
        if (StockManager.Instance == null) { return null; }
        Stock biggest = null; decimal most = 0m;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || stock.IsDelisted || stock.currentPrice <= 0m) { continue; }
            decimal value = bot.GetOwnedVolume(stock) * stock.currentPrice;
            if (value > most) { most = value; biggest = stock; }
        }
        return biggest;
    }
    /// <summary>묻는 말의 답을 이 봇이 아는 사실에 맞춰 고른다. 답의 갈래(key)와, 대사에 채울 종목·기사 제목·평단을 정한다.</summary>
    private void ResolveTalkFacts(TradeBot bot, ref string key, ref Stock stock, ref string news, ref decimal mine)
    {
        bool trading = day.Phase == MarketPhase.Trading;
        switch (key)
        {
            case "news": case "newsNight":
            {
                bool asked = stock != null; var article = LatestNews(stock);
                if (article != null) { news = GameLocale.Text(article.title); if (stock == null) { stock = StockBySymbol(article.symbol); } key = asked ? "news.stock" : "news.has"; }
                else { key = asked ? "news.stockNone" : key == "newsNight" ? "news.night" : "news.none"; }
                break;
            }
            case "market":
            {
                if (!trading) { key = "market.closed"; break; }
                var top = TopMover(out decimal move, out int ups, out int downs);
                key = ups + downs == 0 ? "market.flat" : ups >= downs * 2 ? "market.up" : downs >= ups * 2 ? "market.down" : "market.mixed";
                if (top != null && Math.Abs(move) >= 1m) { stock = top; }
                break;
            }
            case "hot":
            {
                if (!trading) { key = "market.closed"; break; }
                // 열에 넷은 자기가 들고 있는 종목을 댄다. 나머지는 지금 가장 크게 움직이는 종목.
                var held = BiggestHolding(bot);
                if (held != null && HypeRandom.Next() < .4) { stock = held; mine = bot.GetAvgPrice(held); key = "hot.mine"; break; }
                var top = TopMover(out decimal move, out _, out _);
                if (top == null || Math.Abs(move) < 1m) { key = "hot.none"; } else { stock = top; key = move > 0m ? "hot.up" : "hot.down"; }
                break;
            }
            case "whaleTalk":
            {
                // 봇은 세력이 누구인지 모른다. 거래가 갑자기 붙었거나 호가창에 큰 벽이 선 종목을 수상하게 볼 뿐이다.
                if (stock != null)
                {
                    float heat = StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock);
                    float lean = trading ? ManipulationTools.Walls(stock).Lean : 0f;
                    key = heat >= 3f || Mathf.Abs(lean) >= .4f ? "whaleTalk.suspect" : "whaleTalk.quiet";
                }
                else
                {
                    var hottest = HottestStock(out float heat);
                    if (trading && hottest != null && heat >= 3f) { stock = hottest; key = "whaleTalk.suspect"; } else { key = "whaleTalk.none"; }
                }
                break;
            }
            case "holdings": case "profit":
            {
                var held = BiggestHolding(bot);
                if (held == null) { key += ".none"; break; }
                stock = held; mine = bot.GetAvgPrice(held);
                decimal result = mine > 0m ? held.currentPrice / mine - 1m : 0m;
                key += result >= .01m ? ".win" : result <= -.01m ? ".lose" : ".flat";
                break;
            }
            case "why":
            {
                if (stock == null) { key = "question"; break; }
                var article = LatestNews(stock); decimal moved = DayMove(stock);
                if (article != null && HypeRandom.Next() < .5) { news = GameLocale.Text(article.title); key = "why.news"; }
                else { key = moved >= 2m ? "why.up" : moved <= -2m ? "why.down" : "why.flat"; }
                break;
            }
            case "follow":
                key = stock != null ? "follow.stock" : "follow";
                break;
        }
    }
}
