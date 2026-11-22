using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어와 봇이 말을 주고받는 흐름.
//
// 1) 봇이 던진 물음을 기억한다. 방에 대고 물은 말("다들 뭐 보고 있음?")과 내게 되물은 말이 대상이다(다른 봇에게 단 답글, 급등락 때 쏟아지는 말은 뺀다).
//    그 뒤 75초 안에 내가 그 방에 답하면 물은 봇이 내 답을 받는다: 종목을 댔으면 그 종목을 자기 생각대로 받고, "ㅇㅇ"·"ㄴㄴ"·"몰라"에도 맞게 받는다.
// 2) 방금 나와 말을 섞은 봇을 기억한다. 45초 안에 내가 또 말하면 그 봇이 먼저 받는다. "왜?"·"진짜?"라고 되물으면 그 봇이 자기 말을 풀어 준다(서너 번까지).
//    내 답을 받은 봇은 가끔 한 번 더 되물어 대화를 잇는다. 되물은 말은 무엇을 물었는지 알고 있어서, 내 답을 그 물음에 맞춰 읽는다.
// 4) 내가 한 말을 기억해 둔다(본다던 종목과 그때 값, 꺼낸 잡담, 매매 버릇). 내가 조용해지면 그것을 들고 먼저 말을 건다.
// 3) 묻는 말에는 봇이 보고 아는 것으로 답한다: 공개된 기사(NewsManager), 당일 등락과 달아오른 종목, 호가창의 벽, 자기 계좌의 보유와 평단.
//    진짜 가치나 세력의 속사정은 봇도 모른다. 모르면 모른다고 한다.
//
// 표시와 친밀감만 바꾼다. 주문을 내거나 봇의 믿는 값을 바꾸지 않는다.
public sealed partial class InvestorSociety
{
    /// <summary>봇의 물음이 열려 있는 시간, 말을 섞은 봇이 다음 말을 먼저 받는 시간(초).</summary>
    private const double QuestionOpenSec = 75, RoomQuestionSec = 45, PartnerSec = 45;
    private sealed class BotQuestion { public int bot, room; public double at; public Stock stock; public string text, type; public bool toPlayer; }
    private sealed class TalkPartner { public int bot, room, depth; public double at; public Stock stock; public string topic; public bool asked, opinion; }
    /// <summary>내가 채팅에서 한 말 가운데 봇들이 기억해 두는 것. 나중에 먼저 말을 걸 때 쓴다.</summary>
    private sealed class PlayerNote
    {
        public int room, lastBot; public long messageId; public double spokeAt;
        public Stock stock; public decimal price; public double stockAt; public bool stockPinged, stockBought, stockSold;
        /// <summary>내가 이미 말해 준 것(밥을 먹었는지, 수익이 났는지, 뭘 들고 있는지 …)과 그 시각. 봇이 그것을 다시 묻지 않게 한다.</summary>
        public Dictionary<string, double> told = new();
        public string topic; public double topicAt;
        /// <summary>내가 마지막으로 꺼낸 잡담의 갈래와 그 시각, 거기에 덧붙인 말의 수("양념으로", "님들은?"을 그 얘기로 읽는 데 쓴다).</summary>
        public string lastTopic; public double lastTopicAt; public int topicFollows;
        public string style; public bool newbie, newbiePinged;
    }
    private readonly PlayerNote playerNote = new();
    /// <summary>나중에 "아까 ○○다더니" 하고 다시 꺼낼 수 있는 잡담의 갈래(그 갈래의 ping.topic 대사가 있다).</summary>
    private static readonly string[] PingTopics = { "food", "sleep", "work", "bored", "angry", "happy", "health", "gloomy", "tired", "scared", "overtime", "dilemma", "quit", "sideline", "drink" };
    /// <summary>내가 그것(ate, profit, hold …)을 15분 안에 이미 말해 주었는가.</summary>
    private bool PlayerTold(string type) => playerNote.told != null && playerNote.told.TryGetValue(type, out double at) && SocialTime.Seconds - at < 900 && SocialTime.Seconds >= at;
    private readonly Queue<string> askedTypes = new();
    private double pingNext; private int pingStreak; private string talkSince;
    /// <summary>확인용: 봇이 내게 먼저 건 말의 수.</summary>
    public int PlayerPings { get; private set; }
    private readonly List<BotQuestion> botQuestions = new();
    private readonly List<TalkPartner> talkPartners = new();
    private readonly List<string> talkLines = new();
    /// <summary>방금 내게 한 답들. 다른 봇이 같은 문장을 곧바로 되풀이하지 않게 한다.</summary>
    private readonly Queue<string> talkRecent = new();
    private string talkSaid;
    /// <summary>확인용: 봇의 물음에 내가 답한 횟수.</summary>
    public int PlayerAnswersHeard { get; private set; }

    // ── 봇의 물음 ──
    /// <summary>글이 물음으로 끝나는가. "나? 그냥 개미임"처럼 중간에만 물음표가 있는 말은 물음이 아니다.</summary>
    private static bool EndsAsking(string text)
    {
        string shown = (text ?? "").TrimEnd(' ', '.', '!', '~', 'ㅋ', 'ㅎ', 'ㅠ', 'ㅜ', 'w', 'ｗ', ')', '^');
        return shown.EndsWith("?", StringComparison.Ordinal) || shown.EndsWith("？", StringComparison.Ordinal);
    }
    /// <summary>봇이 올린 글이 물음이면 기억해 둔다. 모든 봇 글이 지나는 길(DeliverSocialMessage)에서 부른다.</summary>
    private void NoteBotQuestion(TradeBot bot, SocialMessage message)
    {
        if (bot == null || bot.Social == null || message == null || string.IsNullOrEmpty(message.text) || message.targetId != 0) { return; }
        bool toPlayer = message.topicEvent == "PLAYER_TALK";
        // 혼잣말처럼 던지는 "쏘나?"(잔움직임 반응)나 기분 한마디는 내게 묻는 말이 아니다. 그 뒤의 내 말을 그 답으로 읽으면 엉뚱한 대꾸가 나온다.
        if (message.topicEvent == "TWITCH" || message.topicEvent == "MOOD") { return; }
        if (message.topicEvent == "HYPE" || !toPlayer && message.replyToId != 0 || message.templateKey == "talk.analyze" || !EndsAsking(message.text)) { return; }
        double now = SocialTime.Seconds; int id = bot.Social.Id, room = message.roomId;
        for (int i = botQuestions.Count - 1; i >= 0; i--)
        { if (now - botQuestions[i].at > QuestionOpenSec || botQuestions[i].bot == id && botQuestions[i].room == room) { botQuestions.RemoveAt(i); } }
        // 되물은 말은 무엇을 물었는지(밥, 수익, 매매 버릇 …)도 적어 둔다. 내 답을 그 물음에 맞춰 읽기 위해서다.
        string type = null, template = message.templateKey ?? ""; int mark = template.IndexOf("askBack.", StringComparison.Ordinal);
        if (mark >= 0) { type = template.Substring(mark + 8); int dot = type.IndexOf('.'); if (dot >= 0) { type = type.Substring(0, dot); } if (type == "stock") { type = null; } }
        else if (template.StartsWith("talk.ping.stock", StringComparison.Ordinal)) { type = "hold"; }
        else if (template.StartsWith("talk.which", StringComparison.Ordinal)) { type = "which"; }
        else if (template.StartsWith("talk.ping.topic.food", StringComparison.Ordinal)) { type = "ate"; }
        botQuestions.Add(new BotQuestion { bot = id, room = room, at = now, stock = StockBySymbol(message.facts?.symbol), text = message.text, toPlayer = toPlayer, type = type });
        while (botQuestions.Count > 8) { botQuestions.RemoveAt(0); }
    }
    /// <summary>확인용: 그 방에서 지금 답을 기다리는 봇의 물음(없으면 null).</summary>
    public string OpenBotQuestionText(int room)
    {
        double now = SocialTime.Seconds;
        for (int i = botQuestions.Count - 1; i >= 0; i--) { if (Open(botQuestions[i], room, now)) { return botQuestions[i].text; } }
        return null;
    }
    private bool Open(BotQuestion question, int room, double now)
        => question.room == room && now - question.at <= (question.toPlayer ? QuestionOpenSec : RoomQuestionSec) && SocialBots.TryGetValue(question.bot, out var bot) && Present(bot, room);
    /// <summary>
    /// 열려 있는 봇의 물음에 내가 답했는가. 답했으면 물은 봇이 받는다.
    /// 내게 한 물음이면 무슨 말이든 답으로 친다. 방에 대고 한 물음(봇들끼리도 주고받는다)은 내 말이 답으로 읽힐 때만 친다:
    /// 종목을 물었는데 그 종목을 말했거나, 종목 없이 물었는데 내가 종목을 댔을 때.
    /// 종목 얘기·맞장구·그냥 하는 말은 여기서 답글까지 정하고 true를 돌려준다. 잡담·웃음은 물은 봇이 먼저 받도록만 해 두고 평소대로 간다.
    /// </summary>
    private bool HearPlayerAnswer(int room, PlayerTalk talk, string text, long id, double now, ref int near)
    {
        if (talk.Asks || talk.Kind == PlayerTalkKind.Insult || talk.Kind == PlayerTalkKind.Analyze || talk.Kind == PlayerTalkKind.Lewd || talk.Kind == PlayerTalkKind.Noise) { return false; }
        // "왜?"·"진짜?"·"no way"처럼 앞말을 되묻는 말도 대답이 아니다(예전에는 "no way"를 "아니오"로 받았다).
        if (talk.AsksBack) { return false; }
        // 내가 새로 묻는 말은 봇의 물음에 대한 대답이 아니다.
        if (talk.Kind == PlayerTalkKind.AskCompany || talk.Kind == PlayerTalkKind.AskCompare || talk.Kind == PlayerTalkKind.AskTerm || talk.Kind == PlayerTalkKind.AskHolders || talk.Kind == PlayerTalkKind.AskPrice || talk.Kind == PlayerTalkKind.AskOutlook || talk.Kind == PlayerTalkKind.AskWhich) { return false; }
        bool names = talk.Kind == PlayerTalkKind.Mention || talk.Kind == PlayerTalkKind.ClaimUp || talk.Kind == PlayerTalkKind.ClaimDown;
        // "그렇구나"·"알겠음"은 알아들었다는 말이지 물음에 대한 "예"가 아니다.
        // 딴 말이 붙은 맞장구("ㅇㅇ 아직 들고 있음", "아니 그게 아니라")는 그 물음에 대한 예·아니오가 아니다.
        bool nods = (talk.Kind == PlayerTalkKind.Agree && !talk.Acks || talk.Kind == PlayerTalkKind.Disagree) && talk.BareNod || talk.Kind == PlayerTalkKind.Dunno;
        int talkingTo = near;
        // 뜻을 알아듣지 못한 말(Statement·Other)은 대답으로 치지 않는다. 예전에는 쳤는데, 그러면 "123123"이나 주소 한 줄에도 "오 답 고마움"이 달렸다.
        // 무엇을 물었는지 아는 물음(밥, 매매 버릇 …)은 아래 AnswerClass가 내 말을 그 물음에 맞춰 읽어 낼 때만 대답이 된다.
        bool direct = names || nods;
        bool soft = talk.Kind == PlayerTalkKind.Topic || talk.Kind == PlayerTalkKind.Laugh || talk.Kind == PlayerTalkKind.Sad || talk.Kind == PlayerTalkKind.Surprise;
        BotQuestion open = null;
        for (int i = botQuestions.Count - 1; i >= 0 && open == null; i--)
        {
            var question = botQuestions[i];
            if (!Open(question, room, now)) { continue; }
            // 방금 다른 봇과 말을 주고받던 중이면, 옆 토론에서 던진 "어느 쪽임?"에 대한 대답으로 읽지 않는다.
            if (question.type == "side" && talkingTo != 0 && question.bot != talkingTo) { continue; }
            if (question.toPlayer ? direct || question.type != null : names && (question.stock == null || question.stock == talk.Stock)) { open = question; }
        }
        if (open == null) { return false; }
        if (open.type == "which")
        {
            // 샀다·팔았다고 알리는 말은 그 물음의 대답이 아니다. 종목 이름을 댔을 때만 대답이다.
            if (talk.Stock == null || talk.Bought || talk.Sold) { return false; }
            botQuestions.Remove(open); PlayerAnswersHeard++; near = open.bot;
            Plan(room, "ask", talk.Stock, id, now + 1.1, open.bot, HypeRandom.Range(1, 3), .8f, sure: true);
            Sway(room, talk.Stock, .4f);
            return true;
        }
        // 무엇을 물었는지 아는 물음이면 내 답을 그 물음에 맞춰 읽는다("밥은 먹었음?"에 "아직" → 안 먹었다).
        string answer = open.type == null ? null : AnswerClass(open.type, talk, text);
        if (answer == null && !direct) { return false; }
        // "뭐 볼 거임?"·"얼마나 됨?"처럼 무엇을 묻는 말에 "ㅇㅇ"·"ㄴㄴ"는 대답이 아니다(예전에는 "그치? 나도 그렇게 생각함"이 달렸다).
        if (answer == null && !names && talk.Kind != PlayerTalkKind.Dunno && (open.type == "watch" || open.type == "howlong" || open.type == "style")) { return false; }
        // 샀다·팔았다고 알리는 말은 봇이 무엇을 물었든 그 얘기로 받는다(아래의 평소 길로 보낸다).
        if (answer == null && (talk.Bought || talk.Sold || talk.WillBuy || talk.WillSell) && open.type != "hold" && open.type != "watch") { return false; }
        botQuestions.Remove(open); PlayerAnswersHeard++; near = open.bot;
        if (SocialBots.TryGetValue(open.bot, out var asker))
        {
            // 대답해 준 사람에게는 조금 마음이 간다.
            asker.Social.Psychology.ReactToPlayerTalk(2f, 0f, "플레이어가 물음에 답함"); asker.Social.Psychology.WarmToPlayer(3f, "플레이어가 물음에 답함");
            TraceTalk("    (" + asker.Social.Nickname + "의 물음에 답함: " + open.text + (answer == null ? "" : "  → " + open.type + "." + answer) + ")");
        }
        if (answer != null)
        {
            if (open.type == "style") { playerNote.style = answer; }
            if (open.type == "howlong" && answer == "new") { playerNote.newbie = true; }
            // 토론에서 "어느 쪽임?"에 답했다: 물은 봇의 편에 서거나 반대편에 선다.
            if (open.type == "side" && debate != null)
            {
                int index = debate.cast.IndexOf(open.bot); bool askerPro = index < 0 || debate.side[index] != 1;
                DebatePlayerSide(answer == "yes" ? askerPro : !askerPro, now);
            }
            Plan(room, "answered." + open.type + "." + answer, talk.Stock, id, now + 1.1, open.bot, 1, 1f, sure: true);
            return true;
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
    /// <summary>되물은 말의 갈래에 맞춰 내 답을 읽는다. 읽히지 않으면 null(그때는 보통의 답으로 받는다).</summary>
    private static string AnswerClass(string type, PlayerTalk talk, string text)
    {
        // 딴 말이 붙은 맞장구("ㅇㅇ 아직 들고 있음")는 예·아니오로 치지 않는다.
        bool yes = talk.Kind == PlayerTalkKind.Agree && !talk.Acks && talk.BareNod, no = talk.Kind == PlayerTalkKind.Disagree && talk.BareNod;
        // 딴 얘기(종목 얘기, 다른 잡담)를 그 물음의 답으로 읽지 않는다: "아직 안 샀음"은 밥을 안 먹었다는 말이 아니다.
        bool simple = talk.Stock == null && (talk.Kind == PlayerTalkKind.Agree || talk.Kind == PlayerTalkKind.Disagree || talk.Kind == PlayerTalkKind.Dunno || talk.Kind == PlayerTalkKind.Statement || talk.Kind == PlayerTalkKind.Other);
        switch (type)
        {
            case "ate":
                if (!simple && talk.Topic != "food" && talk.Topic != "delivery" && talk.Topic != "diet") { return null; }
                return no || PlayerTalk.Says(text, PlayerTalk.NotYet) || PlayerTalk.Says(text, PlayerTalk.Hungry) ? "no" : yes || talk.Topic == "food" || PlayerTalk.Says(text, PlayerTalk.Ate) ? "yes" : null;
            case "howlong" when !simple && talk.Topic != "newbie" && talk.Topic != "career": return null;
            case "profit": return no || talk.Kind == PlayerTalkKind.Lament ? "no" : yes || talk.Kind == PlayerTalkKind.Brag ? "yes" : null;
            case "style": return talk.Style;
            case "howlong": return PlayerTalk.Says(text, PlayerTalk.LongAgo) ? "old" : talk.Topic == "newbie" || PlayerTalk.Says(text, PlayerTalk.NewHere) ? "new" : null;
            case "side": return yes || talk.Kind == PlayerTalkKind.ClaimUp ? "yes" : no || talk.Kind == PlayerTalkKind.ClaimDown ? "no" : null;
            case "watch": case "hold": return talk.Stock != null ? null : no || talk.Kind == PlayerTalkKind.Dunno || PlayerTalk.Says(text, PlayerTalk.Nothing) ? "none" : null;
        }
        return null;
    }

    // ── 내가 한 말을 기억했다가 먼저 말 걸기 ──
    /// <summary>내가 방금 한 말에서 기억해 둘 것을 적는다: 어느 종목을 얼마에 말했는지, 무슨 잡담을 꺼냈는지, 매매 버릇.</summary>
    private void NotePlayerLine(int room, PlayerTalk talk, long id, double now)
    {
        var note = playerNote;
        note.room = room; note.messageId = id; note.spokeAt = now;
        pingStreak = 0; pingNext = now + 50 + HypeRandom.Next() * 40;
        // 같은 종목을 또 말한 것(봇이 물어서 답한 것 포함)은 새 얘기가 아니다. 처음 말한 때의 값을 그대로 두고, 이미 물어봤으면 다시 묻지 않는다.
        if (talk.Stock != null && talk.Stock.currentPrice > 0m && (talk.Stock != note.stock || now - note.stockAt > 900))
        { note.stock = talk.Stock; note.price = talk.Stock.currentPrice; note.stockAt = now; note.stockPinged = false; note.stockBought = note.stockSold = false; }
        if (talk.Stock != null) { if (talk.Bought) { note.stockBought = true; note.stockSold = false; } else if (talk.Sold) { note.stockSold = true; note.stockBought = false; } }
        if (talk.Kind == PlayerTalkKind.Topic && Array.IndexOf(PingTopics, talk.Topic) >= 0) { note.topic = talk.Topic; note.topicAt = now; }
        // 이미 말해 준 것은 적어 둔다. 봇이 되물을 때 그것은 다시 묻지 않는다.
        note.told ??= new Dictionary<string, double>();
        if (talk.Topic == "food" || talk.Topic == "delivery") { note.told["ate"] = now; }
        if (talk.Kind == PlayerTalkKind.Brag || talk.Kind == PlayerTalkKind.Lament || talk.Topic == "account" || talk.Topic == "breakeven") { note.told["profit"] = now; }
        if (talk.Stock != null) { note.told["watch"] = now; if (talk.Bought || talk.Kind == PlayerTalkKind.Brag || talk.Kind == PlayerTalkKind.Lament) { note.told["hold"] = now; } }
        if (talk.Topic == "sideline" || talk.Topic == "bought" || talk.Topic == "sold") { note.told["hold"] = now; }
        if (talk.Topic == "career") { note.told["howlong"] = now; }
        if (talk.Kind == PlayerTalkKind.Topic && talk.Topic != null) { note.lastTopic = talk.Topic; note.lastTopicAt = now; note.topicFollows = 0; }
        else if (talk.Kind != PlayerTalkKind.Statement && !talk.AsksSame) { note.lastTopic = null; }
        if (talk.Topic == "style") { note.told["style"] = now; }
        if (talk.Topic == "newbie") { note.newbie = true; }
        if (talk.Style != null) { note.style = talk.Style; }
    }
    /// <summary>
    /// 내가 한동안 조용하면 봇이 먼저 말을 건다. 장중에, 이 판에서 내가 채팅을 한 적이 있을 때만, 45초 넘게 조용할 때, 80~160초에 한 번.
    /// 내가 받지 않으면 두 번까지만 건다. 할 말은 내가 했던 말에서 찾는다(본다던 종목이 그 뒤 얼마나 움직였는지, 배고프다던 얘기 따위).
    /// </summary>
    private void TickPlayerPing(double now)
    {
        var note = playerNote;
        // 토론이 한창일 때는 따로 말을 걸지 않는다(그때는 토론 속에서 나를 끌어들인다).
        if (debate != null) { return; }
        if (note.spokeAt <= 0 || now < pingNext || pingStreak >= 2 || day.Phase != MarketPhase.Trading || talkReplies.Count > 0 || now - note.spokeAt < 45 || now - note.spokeAt > 900 || !PlayerCanWrite(note.room)) { return; }
        pingNext = now + 80 + HypeRandom.Next() * 80; pingStreak++;
        // 이미 팔았다고 한 종목을 두고 "샀음?" 하고 묻지 않는다.
        bool watching = note.stock != null && note.stock.AcceptsOrders && now - note.stockAt < 900 && !note.stockPinged && !note.stockSold;
        Plan(note.room, "ping", watching ? note.stock : null, 0, now + .2, note.lastBot, 1, 1f, sure: true);
    }

    // ── 말을 섞은 봇 ──
    private TalkPartner TalkPartnerIn(int room, double now)
    {
        foreach (var partner in talkPartners)
        { if (partner.room == room && now - partner.at <= PartnerSec && SocialBots.TryGetValue(partner.bot, out var bot) && Present(bot, room)) { return partner; } }
        return null;
    }
    /// <summary>
    /// 봇이 내 말에 답한 뒤의 일: 그 봇을 말 상대로 기억한다. 내 답을 받은 봇은 가끔 한 번 더 되물어 대화를 잇는다(이어질수록 드물게, 네 번까지).
    /// "왜?"에 질린 봇은 대화를 접는다.
    /// </summary>
    private void AfterTalkReply(TalkReply reply, TradeBot bot, string key, Stock stock, double now)
    {
        int id = bot.Social.Id, depth = 0;
        for (int i = talkPartners.Count - 1; i >= 0; i--)
        {
            var old = talkPartners[i];
            if (old.room == reply.room && old.bot == id && now - old.at <= PartnerSec) { depth = old.depth; }
            if (old.room == reply.room || now - old.at > PartnerSec) { talkPartners.RemoveAt(i); }
        }
        if (key.StartsWith("follow.tired", StringComparison.Ordinal)) { return; }
        bool deeper = key.StartsWith("follow", StringComparison.Ordinal) || key.StartsWith("askBack", StringComparison.Ordinal);
        // 잡담의 갈래를 기억해 두면, 내가 이어서 한마디 했을 때 그 얘기로 받을 수 있다.
        string topic = key.StartsWith("topic.", StringComparison.Ordinal) ? key.Substring(6) : null;
        if (topic != null) { int dot = topic.IndexOf('.'); if (dot >= 0) { topic = topic.Substring(0, dot); } }
        bool asked = key.StartsWith("askBack", StringComparison.Ordinal) || key.StartsWith("ping", StringComparison.Ordinal) || key.StartsWith("which", StringComparison.Ordinal);
        // 방금 한 말이 종목·시세를 두고 한 의견이었는가("왜?"라고 되물으면 까닭을 댈 수 있는 말).
        bool opinion = OpinionKey(key);
        talkPartners.Add(new TalkPartner { bot = id, room = reply.room, at = now, stock = stock, depth = deeper ? depth + 1 : depth, topic = topic, asked = asked, opinion = opinion && !asked });
        playerNote.lastBot = id;
        if (key.StartsWith("ping", StringComparison.Ordinal)) { PlayerPings++; }
        if (key.StartsWith("answered", StringComparison.Ordinal) && depth < 4 && HypeRandom.Next() < .6 - depth * .13)
        { Plan(reply.room, "askBack", stock, 0, now + 1.4 + HypeRandom.Next(), id, 1, 1f, sure: true); }
    }

    // ── 누가 말하나 ──
    /// <summary>
    /// 답할 봇을 고른다. 맡은 봇(물은 봇, 방금 말을 섞은 봇)이 방에 있으면 그 봇이다.
    /// 아니면 지금 채팅을 보고 있고 방금 말하지 않은 반말 봇 가운데 하나. 그런 봇이 없으면 자리를 비운 봇, 그래도 없으면 존댓말 봇까지 넓힌다(작은 방에서도 답이 나오게).
    /// </summary>
    private TradeBot PickTalkSpeaker(TalkReply reply, double now)
    {
        int formal = Config.Int("styleFormalMin");
        // 존댓말 봇은 자기가 물은 말의 답만 받는다. 잡담을 맡기면 "그렇군요"만 되풀이한다.
        if (reply.onlyBot != 0 && SocialBots.TryGetValue(reply.onlyBot, out var wanted) && wanted != null && wanted.Social != null && Present(wanted, reply.room) && !IsTyping(wanted) &&
            (wanted.Social.Psychology.Traits.Politeness < formal || reply.key.StartsWith("answered", StringComparison.Ordinal))) { return wanted; }
        float clock = SessionClock.Time; var odd = ResolveOddball();
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
            if (hypeSpeakers.Count > 0)
            {
                // 나를 좋아하는 봇일수록 내 말을 먼저 받는다. 싫어하는 봇은 잘 끼지 않는다.
                double total = 0;
                foreach (var candidate in hypeSpeakers) { total += FondWeight(candidate); }
                double roll = HypeRandom.Next() * total;
                foreach (var candidate in hypeSpeakers) { roll -= FondWeight(candidate); if (roll <= 0) { return candidate; } }
                return hypeSpeakers[hypeSpeakers.Count - 1];
            }
        }
        return null;
    }
    private static double FondWeight(TradeBot bot)
    {
        float fondness = bot.Social.Psychology.Fondness;
        return fondness >= 0f ? 1 + fondness / 20d : 1 / (1 + -fondness / 30d);
    }
    /// <summary>
    /// 그 봇이 방금 한 말이 "왜?"라고 되물으면 까닭을 댈 수 있는 의견이었는가.
    /// 좋게 본다·비싸다·더 간다 같은 판단만 의견이다. "지금 201,000돈이네", "샀구나. 성투" 같은 말은 의견이 아니다
    /// (예전에는 종목이 들어간 말이면 다 의견으로 쳐서, "샀구나"에 "왜?" 하면 "근거는 없고 감임"이 달렸다).
    /// </summary>
    private static bool OpinionKey(string key)
    {
        if (key.EndsWith(".bull", StringComparison.Ordinal) || key.EndsWith(".bear", StringComparison.Ordinal) || key.EndsWith(".agree", StringComparison.Ordinal) || key.EndsWith(".doubt", StringComparison.Ordinal)) { return true; }
        foreach (string start in new[] { "outlook.up", "outlook.down", "advice", "whaleTalk.suspect", "why.", "compare", "follow.stock", "market.up", "market.down", "hot.up", "hot.down", "askPick" })
        { if (key.StartsWith(start, StringComparison.Ordinal)) { return true; } }
        return key == "follow" || key == "compare";
    }
    /// <summary>묻는 말에 다는 답인가(존댓말 봇이 받을 때 말투를 고르는 데 쓴다).</summary>
    private static bool AsksKey(string key) => key == "question" || key == "all" || key == "follow" || key == "how" || key == "market" || key == "hot" || key == "whaleTalk" || key == "holdings" || key == "profit" || key == "why" ||
        key == "which" || key == "outlook" || key == "holders" || key == "price" || key == "company" || key == "compare" || key.EndsWith(".q", StringComparison.Ordinal) || key.StartsWith("topic.term_", StringComparison.Ordinal) ||
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
        // 방금 나온 문장은 뺀다. 그러면 남는 게 없을 때만 그대로 쓴다.
        int kept = talkLines.Count;
        for (int i = talkLines.Count - 1; i >= 0 && kept > 1; i--) { if (talkRecent.Contains(talkLines[i])) { talkLines.RemoveAt(i); kept--; } }
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
            case "company":
            {
                // 종목 정보에 적힌 업종. 모르면 모른다고 한다.
                var profile = stock == null || NewsManager.Instance == null || NewsManager.Instance.Writing == null ? null : NewsManager.Instance.Writing.Profile(stock.symbol);
                string sector = profile == null || string.IsNullOrWhiteSpace(profile.sector) ? null : GameLocale.Text(profile.sector);
                if (sector == null) { key = "company.none"; } else { news = sector; }
                break;
            }
            case "outlook":
            {
                // 종목 없이 앞날을 묻는 말: 이 봇의 지금 기분대로 본다. 욕심이 크면 더 간다고, 겁이 나 있으면 더 빠진다고 한다.
                var mind = bot.Social.Psychology;
                key = mind.Fear >= 55f ? "outlook.down" : mind.Greed >= 60f ? "outlook.up" : "outlook.unsure";
                break;
            }
            case "holders":
            {
                // 그 종목을 실제로 들고 있는가.
                if (stock == null) { key = "question"; break; }
                if (bot.GetOwnedVolume(stock) <= 0m) { key = "holders.none"; break; }
                mine = bot.GetAvgPrice(stock);
                decimal result = mine > 0m ? stock.currentPrice / mine - 1m : 0m;
                key = result >= .01m ? "holders.win" : result <= -.01m ? "holders.lose" : "holders.flat";
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
            {
                // 종목 얘기였으면 이 봇이 그 종목을 실제로 어떻게 보는지에 맞춰 까닭을 댄다.
                if (stock == null) { break; }
                decimal believed = bot.BelievedPrice(stock), price = stock.currentPrice;
                key = believed > price * 1.03m ? "follow.stock.bull" : believed < price * .97m ? "follow.stock.bear" : "follow.stock.neutral";
                break;
            }
            case "askBack":
            {
                if (stock != null && HypeRandom.Next() < .35) { key = "askBack.stock"; break; }
                // 최근에 물은 것은 다시 묻지 않는다. 이미 아는 것(매매 버릇, 초보인지)도 다시 묻지 않는다.
                string[] types = { "watch", "hold", "ate", "profit", "style", "howlong" }; string chosen = null;
                for (int tries = 0; tries < 12 && (chosen == null || askedTypes.Contains(chosen) || PlayerTold(chosen) || chosen == "style" && playerNote.style != null || chosen == "howlong" && playerNote.newbie); tries++)
                { chosen = types[HypeRandom.Range(0, types.Length)]; }
                askedTypes.Enqueue(chosen); while (askedTypes.Count > 4) { askedTypes.Dequeue(); }
                key = "askBack." + chosen;
                break;
            }
            case "ping":
            {
                var note = playerNote; double now = SocialTime.Seconds;
                if (stock != null && note.price > 0m)
                {
                    // 내가 그 종목을 말한 뒤로 값이 얼마나 움직였는가.
                    decimal since = (stock.currentPrice / note.price - 1m) * 100m; note.stockPinged = true;
                    talkSince = Math.Abs(since).ToString("0.#", SocialCulture);
                    // 샀다고 한 종목이면 "샀음?"이 아니라 "아까 산 거 어떻게 됐음?"으로 묻는다.
                    string held = note.stockBought ? "ping.bought" : "ping.stock";
                    key = since >= 1.5m ? held + "Up" : since <= -1.5m ? held + "Down" : held;
                    break;
                }
                stock = null;
                if (note.topic != null && now - note.topicAt < 900) { key = "ping.topic." + note.topic; note.topic = null; break; }
                if (note.newbie && !note.newbiePinged) { note.newbiePinged = true; key = "ping.newbie"; }
                break;
            }
        }
    }
}
