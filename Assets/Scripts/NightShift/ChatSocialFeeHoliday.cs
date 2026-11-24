using System;
using System.Collections.Generic;

/// <summary>
/// 수수료 0% 이벤트 날의 채팅. 08:30에 이벤트가 시작되면 공개 라운지가 그 얘기로 웅성거린다.
///  - 한마디: "오늘 수익 내야하는데...", "오늘 대장 없냐?" 같은 짧은 말.
///  - 주고받기: 한 사람이 묻고 다른 사람들이 차례로 받는다("이거 이벤트 언제까지함?" → "오늘만 한댔음" → "구라치지마 ㅅㅂ" → "진짜라니까;").
///    줄마다 다른 사람이 말하고, 앞사람의 말에 답글로 달린다.
/// 장이 열리기 전에는 몇 초마다, 장중에는 이삼십 초마다 나와서 그날의 대화는 이 얘기가 주를 이룬다. 평소의 말들도 그대로 섞인다.
/// 말만 한다. 시세·계좌·믿음은 건드리지 않는다. 글은 그 순간의 언어로 적는다(한국어·영어·일본어를 각각 적어 두었다).
/// </summary>
public sealed partial class InvestorSociety
{
    private sealed class HolidayLine
    {
        public double due;
        /// <summary>주고받기의 번호와 몇 번째 줄인지. 한마디면 thread가 -1이고 step이 한마디의 번호다.</summary>
        public int thread, step;
        public long replyTo;
        public int exclude;
    }

    private static readonly (string ko, string en, string ja)[] HolidayOpeners =
    {
        ("오늘 수익 내야하는데...", "gotta make money today...", "今日こそ利益出さないと..."),
        ("오늘 대장 없냐?", "who's the leader stock today?", "今日の主役どれ?"),
        ("수수료 0%래 오늘", "0% fee today btw", "今日手数料0%だって"),
        ("오늘 같은 날 안 하면 바보지", "you'd be dumb to sit out today", "今日やらないのは損でしょ"),
        ("수수료 없는 날이라 단타 각", "no fee = scalping day", "手数料なしならスキャ日和"),
        ("거래소가 웬일이냐", "since when is the exchange this nice", "取引所どうした急に"),
        ("오늘 거래량 터지겠네", "volume's gonna explode today", "今日は出来高すごそう"),
        ("0%면 팔 때 한 푼도 안 떼는 거 맞지?", "0% means nothing taken when I sell, right?", "0%って売っても何も引かれないよね?"),
        ("오늘은 백 번 사고팔아도 공짜", "a hundred round trips, all free today", "今日は百回売買しても無料"),
        ("세력들 오늘 가만 안 있을 듯", "the whales won't sit still today", "今日は仕手筋が動きそう"),
        ("오늘 뭐 사냐 다들", "what's everyone buying today", "みんな今日なに買う?"),
        ("이런 날 꼭 물리더라 나는", "days like this are when I get stuck", "こういう日に限って捕まるんだよな"),
        ("수수료 아껴서 치킨 사 먹어야지", "saving the fee for fried chicken", "浮いた手数料でチキン食べよ"),
        ("오늘만 기다렸다", "been waiting for this day", "この日を待ってた"),
        ("0%라고 막 사면 안 되는데", "0% doesn't mean buy everything though", "0%だからって何でも買っちゃだめだけど"),
        ("장 열리면 바로 들어간다", "going in the second it opens", "寄り付いたらすぐ入る"),
    };

    private static readonly (string ko, string en, string ja)[][] HolidayThreads =
    {
        new[] { ("이거 이벤트 언제까지함?", "how long does this event last?", "このイベントいつまで?"), ("오늘만 한댔음", "today only they said", "今日だけらしい"), ("구라치지마 ㅅㅂ", "no way, stop lying", "嘘つくなよ"), ("진짜라니까;", "I'm serious;", "本当だって;") },
        new[] { ("수수료 0% 진짜임?", "is the 0% fee real?", "手数料0%ってマジ?"), ("거래소 공지 떴음", "exchange posted a notice", "取引所の告知出てる"), ("그럼 오늘 무조건 한다", "then I'm trading today no matter what", "じゃあ今日は絶対やる") },
        new[] { ("오늘 대장 뭐냐", "what's the leader today", "今日の主役なに"), ("아직 아무도 모름", "nobody knows yet", "まだ誰もわからん"), ("세력이 정해주겠지 뭐", "the whales will decide for us", "仕手筋が決めてくれるでしょ"), ("그걸 왜 기다림 ㅋㅋ", "why wait for that lol", "それ待つの草") },
        new[] { ("평소엔 수수료 얼마였지", "what's the fee normally", "普段の手数料いくらだっけ"), ("팔 때 0.05%", "0.05% when you sell", "売るとき0.05%"), ("그거 아끼자고 이 난리냐", "all this fuss to save that?", "それ節約するためにこの騒ぎ?"), ("티끌 모아 태산임", "every little bit adds up", "ちりも積もれば山") },
        new[] { ("오늘 세력 붙는 종목 찍어주라", "someone call the stock the whales are on", "仕手が付く銘柄教えて"), ("그걸 알면 내가 여기 있겠냐", "if I knew I wouldn't be here", "知ってたらここにいない"), ("ㅋㅋㅋ 인정", "lol fair", "ｗｗ それな") },
        new[] { ("오늘 몇 번 사고팔 거임?", "how many trades you doing today?", "今日何回売買する?"), ("손가락 닳을 때까지", "until my fingers wear out", "指がすり減るまで"), ("그러다 계좌가 먼저 닳음", "your account will wear out first", "先に口座がすり減るぞ") },
        new[] { ("이벤트 날은 원래 변동 큼?", "are event days always this wild?", "イベントの日って値動き大きい?"), ("거래량 늘면 위아래 다 열림", "more volume, bigger moves both ways", "出来高増えたら上も下も開く"), ("아래는 안 열렸으면", "hope it's only up", "下は開かないでほしい"), ("그건 니 희망이고", "that's just your wish", "それはお前の願望") },
        new[] { ("오늘 못 벌면 진짜 접는다", "if I don't profit today I quit", "今日勝てなかったら本当にやめる"), ("저번에도 그 말 했잖아", "you said that last time", "この前も言ってたよね"), ("이번엔 진짜", "for real this time", "今度こそ本当") },
        new[] { ("수수료 0%면 거래소는 뭐 먹고 삼", "how does the exchange eat at 0%", "0%で取引所はどうやって食ってくの"), ("내일부터 다시 뗌", "they take it again tomorrow", "明日からまた取る"), ("미끼네 미끼", "it's bait", "餌じゃん"), ("알고도 무는 게 우리지", "and we bite anyway", "わかってて食いつくのが俺ら") },
        new[] { ("다들 총알 얼마나 남음?", "how much dry powder you all got?", "みんな余力どれくらい?"), ("어제 다 썼는데", "spent it all yesterday", "昨日全部使った"), ("하필 오늘이 이벤트라니", "of course the event is today", "よりによって今日イベントか") },
    };

    private readonly List<HolidayLine> holidayPending = new();
    private readonly Queue<int> holidaySpeakers = new(), holidayRecent = new();
    private readonly System.Random holidayRandom = new(51907);
    private int holidayDay = -1;
    private double holidayNext = double.NaN;
    /// <summary>확인용: 이벤트 얘기가 채팅에 올라온 줄 수.</summary>
    public int FeeHolidayLines { get; private set; }

    /// <summary>이벤트가 시작됐다(그날 08:30). 처음 삼십 초 동안 여러 사람이 한꺼번에 떠든다.</summary>
    public void BeginFeeHolidayTalk(int day)
    {
        holidayDay = day; holidayPending.Clear();
        SyncHolidayClock();
        double now = SocialTime.Seconds, due = now + 1.5;
        for (int i = 0; i < 5; i++)
        {
            holidayPending.Add(new HolidayLine { due = due, thread = -1, step = PickHolidayOpener() });
            due += 2.5 + holidayRandom.NextDouble() * 4.0;
        }
        // 그 사이에 주고받기 둘이 끼어든다. 첫 번째는 언제나 "언제까지 함?"이다.
        holidayPending.Add(new HolidayLine { due = now + 6.0, thread = 0, step = 0 });
        holidayPending.Add(new HolidayLine { due = now + 19.0, thread = 1 + holidayRandom.Next(HolidayThreads.Length - 1), step = 0 });
        holidayNext = due + 6.0;
    }

    /// <summary>장이 열리기 전에는 대화의 시계가 드문드문 맞춰진다. 예약한 줄이 제때 나가도록 여기서 맞춘다.</summary>
    private void SyncHolidayClock()
    {
        var market = MarketDayController.Instance;
        if (market != null) { SocialTime.Set(market.Day, market.GameMinute, SessionClock.Time); }
    }
    private int PickHolidayOpener()
    {
        int pick = holidayRandom.Next(HolidayOpeners.Length);
        for (int tries = 0; tries < 8 && holidayRecent.Contains(pick); tries++) { pick = holidayRandom.Next(HolidayOpeners.Length); }
        holidayRecent.Enqueue(pick);
        while (holidayRecent.Count > 9) { holidayRecent.Dequeue(); }
        return pick;
    }

    /// <summary>예약된 줄을 내보내고, 그날 내내 이벤트 얘기를 이어 간다.</summary>
    public void TickFeeHolidayTalk()
    {
        var market = MarketDayController.Instance;
        if (!SocialReady || market == null || holidayDay != market.Day || !FeeHoliday.Active) { return; }
        bool trading = market.Phase == MarketPhase.Trading;
        if (!trading && market.Phase != MarketPhase.PreOpen) { holidayPending.Clear(); return; }

        SyncHolidayClock();
        double now = SocialTime.Seconds;
        for (int i = 0; i < holidayPending.Count; i++)
        {
            var line = holidayPending[i];
            // 불러오기로 시계가 되돌아갔으면 기다리던 줄은 버린다.
            if (line.due - now > 120) { holidayPending.RemoveAt(i--); continue; }
            if (line.due > now) { continue; }
            holidayPending.RemoveAt(i);
            bool single = line.thread < 0;
            var text = single ? HolidayOpeners[line.step] : HolidayThreads[line.thread][line.step];
            long id = SpeakFeeHoliday(text, line.replyTo, line.exclude, out int speaker);
            // 주고받기는 앞사람의 말에 다른 사람이 이어서 답한다.
            if (id != 0 && !single && line.step + 1 < HolidayThreads[line.thread].Length)
            { holidayPending.Add(new HolidayLine { due = now + 2.5 + holidayRandom.NextDouble() * 4.0, thread = line.thread, step = line.step + 1, replyTo = id, exclude = speaker }); }
            return;
        }

        if (double.IsNaN(holidayNext) || holidayNext - now > 120) { holidayNext = now + 5; }
        if (now < holidayNext) { return; }
        // 장이 열리기 전에는 몇 초마다, 장중에는 이삼십 초마다.
        holidayNext = now + (trading ? 14 + holidayRandom.NextDouble() * 18 : 5 + holidayRandom.NextDouble() * 6);
        if (holidayRandom.NextDouble() < .4) { holidayPending.Add(new HolidayLine { due = now, thread = holidayRandom.Next(HolidayThreads.Length), step = 0 }); }
        else { holidayPending.Add(new HolidayLine { due = now, thread = -1, step = PickHolidayOpener() }); }
    }

    /// <summary>공개 라운지에 있는 사람 가운데 한 명이 말한다. 방금 말한 사람과 앞줄을 쓴 사람은 고르지 않는다. 말한 사람이 없으면 0.</summary>
    private long SpeakFeeHoliday((string ko, string en, string ja) text, long replyTo, int exclude, out int speaker)
    {
        speaker = 0; TradeBot chosen = null; int seen = 0;
        foreach (var bot in socialBots)
        {
            if (bot == null || bot.Social == null || bot.Social.Id == exclude || bot.KnowsRealValue || !Present(bot, 0) || holidaySpeakers.Contains(bot.Social.Id)) { continue; }
            seen++;
            if (holidayRandom.Next(seen) == 0) { chosen = bot; }
        }
        if (chosen == null) { return 0; }

        string phrase = GameLocale.Code == "ja" ? text.ja : GameLocale.Code == "en" ? text.en : text.ko;
        var facts = new SocialFacts { at = SocialTime.GameMinute };
        var message = new SocialMessage
        {
            id = ChatSocialIds.Next(), roomId = 0, senderId = chosen.Social.Id, targetId = 0,
            at = SocialTime.GameMinute, realAt = SocialTime.Seconds, replyToId = replyTo, facts = facts, topicEvent = "FEE_HOLIDAY", templateKey = "FEEHOLIDAY",
            act = replyTo != 0 ? SocialAct.ANSWER : SocialAct.REACT,
            debug = new SocialMessageDebug { trigger = "FEE_HOLIDAY", speechKey = "FEEHOLIDAY", weights = Array.Empty<SocialWeight>(), slots = facts }
        };
        message.text = phrase;
        chosen.Social.LastSpeech = (float)SocialTime.Seconds;
        speaker = chosen.Social.Id;
        holidaySpeakers.Enqueue(speaker);
        while (holidaySpeakers.Count > 12) { holidaySpeakers.Dequeue(); }
        FeeHolidayLines++;
        SocialMessagePublished?.Invoke(message);
        return message.id;
    }
}
