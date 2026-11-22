using System;
using System.Collections.Generic;
using UnityEngine;

// 봇이 내게 하는 답과 토론의 말, 기분 한마디, 시세에 대한 호들갑은 정해진 순간에 곧바로 뜨지 않는다. 사람이 읽고, 치고, 올리는 만큼 걸린다.
//   말하기로 한 순간 → "○○님이 채팅 입력 중..."이 뜨고 → 글이 완성되면 글 길이만큼(한 글자에 0.085초쯤, 길어도 11초) 더 기다렸다가 올라온다.
// 짧은 "ㅋㅋ"는 금방, 긴 글은 한참 걸린다. 여럿이 한꺼번에 치고 있으면 "3명이 입력 중..."이 된다.
//
// 글을 만드는 일(문장 고르기·짜 맞추기·빈칸 채우기·오타·걸리는 시간 재기)은 작업 스레드가 한다(ChatScheduler).
// 여기(메인 스레드)는 재료를 담아 맡기고, 완성된 글을 거둬 올릴 목록(pendingLines)에 넣고, 시간이 되면 채팅방에 올린다.
public sealed partial class InvestorSociety
{
    private sealed class PendingLine
    {
        public int bot, room; public string text, key, topicEvent; public Stock stock; public long replyTo;
        public double startAt, postAt; public bool split; public Debate owner;
    }
    private readonly List<PendingLine> pendingLines = new();
    private static string[] typoKeep; private static double typoKeepAt = double.MinValue; private static string typoKeepLanguage;
    private static readonly Unity.Profiling.ProfilerMarker ScheduleMarker = new("DonDon.Talk.Schedule");
    /// <summary>글을 치는 데 걸리는 시간(초). 글자 수에 비례한다. 나를 부르는 멘션은 세지 않는다. (작업 스레드에서도 부른다.)</summary>
    public static double TypingSeconds(string text)
    {
        if (string.IsNullOrEmpty(text)) { return .5; }
        int letters = 0;
        foreach (char c in text) { if (!char.IsWhiteSpace(c)) { letters++; } }
        foreach (string you in YouMentions) { if (text.StartsWith(you, StringComparison.Ordinal)) { letters -= you.Length; break; } }
        // 한글·가나·한자는 한 글자가 영문 두세 글자만큼 손이 간다.
        bool wide = false; foreach (char c in text) { if (c >= 0x1100) { wide = true; break; } }
        return Math.Min(11.0, .45 + Math.Max(1, letters) * (wide ? .085 : .04));
    }
    /// <summary>이미 정해진 글을 쓰기 시작하게 한다. 다 쓰면 올라간다. 올라갈 시각(어림)을 돌려준다.</summary>
    private double QueueLine(TradeBot bot, int room, string text, string key, Stock stock, long replyTo, string topicEvent, double now, bool split = false, Debate owner = null)
    {
        var work = NewWork(bot, room, key, stock, replyTo, topicEvent, now, split, owner);
        work.text = text; ChatScheduler.Submit(work); TypingVersion++;
        return now + work.startDelay + TypingSeconds(text);
    }
    /// <summary>
    /// 글을 작업 스레드에서 만들게 맡긴다. compose는 넘겨받은 난수와 미리 담아 둔 재료(문자열·배열·복사한 사전)만 써야 한다.
    /// extraDelay만큼 더 뜸을 들인다(여럿이 같은 일에 한마디씩 할 때 순서를 벌린다). ready는 글이 완성됐을 때 메인 스레드에서 불린다.
    /// </summary>
    private void QueueCompose(TradeBot bot, int room, Func<System.Random, string> compose, string key, Stock stock, long replyTo, string topicEvent, double now, double extraDelay = 0, Action<string> ready = null)
    {
        var work = NewWork(bot, room, key, stock, replyTo, topicEvent, now, false, null);
        work.compose = compose; work.startDelay += extraDelay; work.ready = ready;
        ChatScheduler.Submit(work); TypingVersion++;
    }
    private ChatWork NewWork(TradeBot bot, int room, string key, Stock stock, long replyTo, string topicEvent, double now, bool split, Debate owner)
    {
        return new ChatWork
        {
            bot = bot.Social.Id, room = room, key = key, stock = stock, replyTo = replyTo, topicEvent = topicEvent, split = split, owner = owner,
            submittedAt = now, startDelay = .3 + HypeRandom.Next() * .9, seed = (int)(HypeRandom.Next() * int.MaxValue),
            keep = TypoKeep(now), typoChance = TypoChance, typoFixChance = TypoFixChance,
        };
    }
    /// <summary>오타를 내면 안 되는 낱말들(종목 이름과 줄임말). 오타는 한국어에서만 낸다. 몇 초에 한 번만 다시 모은다.</summary>
    private static string[] TypoKeep(double now)
    {
        if (GameLocale.Code != "ko" || StockManager.Instance == null) { return null; }
        if (typoKeep != null && typoKeepLanguage == GameLocale.Code && Math.Abs(now - typoKeepAt) < 5) { return typoKeep; }
        var words = new List<string>();
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null) { continue; }
            string name = GameLocale.Text(stock.stockName ?? "");
            if (name.Length > 0) { words.Add(name); }
            foreach (string nickname in StockNicknames.All(stock.symbol)) { if (!string.IsNullOrEmpty(nickname)) { words.Add(nickname); } }
        }
        typoKeep = words.ToArray(); typoKeepAt = now; typoKeepLanguage = GameLocale.Code;
        return typoKeep;
    }
    private bool WritingLine(TradeBot bot)
    {
        if (bot == null || bot.Social == null) { return false; }
        foreach (var line in pendingLines) { if (line.bot == bot.Social.Id) { return true; } }
        foreach (var work in ChatScheduler.Flying) { if (work.bot == bot.Social.Id) { return true; } }
        return false;
    }
    private void TickPendingLines()
    {
        if (day == null) { return; }
        double now = SocialTime.Seconds;
        using (ScheduleMarker.Auto()) { TakeWrittenLines(now); }
        if (pendingLines.Count == 0) { return; }
        for (int i = 0; i < pendingLines.Count;)
        {
            var line = pendingLines[i];
            // 불러오기 등으로 시계가 뒤로 갔으면 기다리지 않고 올린다.
            if (line.postAt > now && line.postAt - now < 30) { i++; continue; }
            pendingLines.RemoveAt(i); TypingVersion++;
            if (!SocialBots.TryGetValue(line.bot, out var bot) || bot == null || !Present(bot, line.room)) { continue; }
            long id;
            if (line.split && line.room == 0) { id = PublishHype(bot, DetachMention(line.text), line.key, line.stock, line.replyTo, 1).last.id; }
            else { id = PostBotLine(bot, line.room, line.text, line.key, line.stock, line.replyTo, line.topicEvent); }
            // 토론의 다음 사람은 이 글에 답을 단다.
            if (line.owner != null && ReferenceEquals(debate, line.owner)) { line.owner.lastId = id; }
        }
    }
    /// <summary>작업 스레드가 완성한 글을 거둬, 글 길이만큼 기다렸다가 올릴 목록에 넣는다.</summary>
    private void TakeWrittenLines(double now)
    {
        while (ChatScheduler.TryTake(out var work))
        {
            TypingVersion++;
            if (work.error != null) { Debug.LogWarning("Chat scheduler could not write a line (" + work.key + "): " + work.error.Message); continue; }
            if (string.IsNullOrEmpty(work.result)) { continue; }
            // 치기 시작하는 때는 맡긴 때에서 뜸만큼 뒤다. 글이 늦게 완성됐으면(드물다) 지금부터 센다.
            double start = Math.Max(work.submittedAt + work.startDelay, now - 5), post = Math.Max(start, now) + work.typeSeconds;
            pendingLines.Add(new PendingLine { bot = work.bot, room = work.room, text = work.result, key = work.key, topicEvent = work.topicEvent, stock = work.stock, replyTo = work.replyTo,
                startAt = start, postAt = post, split = work.split, owner = work.owner as Debate });
            if (work.typoRight != null)
            {
                TyposMade++;
                TraceDebate("    (오타: " + work.typoRight + " → " + work.typoWrong + (work.fix != null ? ", 곧 고침" : "") + ")");
            }
            if (work.fix != null) { pendingLines.Add(new PendingLine { bot = work.bot, room = work.room, text = work.fix, key = "typo.fix", topicEvent = "TYPO", startAt = post + .4, postAt = post + 1.0 + work.fixSeconds }); }
            work.ready?.Invoke(work.result);
        }
    }
}
