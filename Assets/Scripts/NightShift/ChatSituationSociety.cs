using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// 상황 판정과 언어별 반응 엔진을 채팅의 여러 장치에 잇는 곳.
//   - 종목마다 2초에 한 번 값을 찍어 당일 기록을 잇는다(TickSituations). 누가 물으면 그 자리에서 읽어 준다(SituationOf).
//   - 시세가 계기인 말(안 본 사이 값이 달라졌다, 가격이 한 계단 움직였다)이 지금 상황에서 말할 거리인지 가린다.
//   - 대화 틀에서 나오는 시세 반응을 번역 대사 대신 그 언어의 대사로 바꾼다(TryNativeMarketLine).
//   - 차트 수다·잔움직임이 상황 대사를 한 줄 달라고 할 때 골라 준다(SituationLine).
//   - 확인용 기록: 환경 변수 DONDON_CHAT_REVIEW에 파일 경로가 있으면 화면에 뜬 봇의 말을 한 줄씩 적는다(ChatReview).

/// <summary>
/// 화면에 뜬 봇의 말을 탭으로 나눈 한 줄씩 파일에 적는다: 게임 시각, 방, 말한 사람, 언어, 장치, 묶음 이름과 상황, 글.
/// 환경 변수 DONDON_CHAT_REVIEW가 비어 있으면 아무것도 하지 않는다(처음 한 번 변수를 읽은 뒤로는 참·거짓만 본다).
/// </summary>
public static class ChatReview
{
    private static bool asked;
    private static StreamWriter writer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Close(); asked = false; }

    public static bool On
    {
        get
        {
            if (!asked) { asked = true; Open(); }
            return writer != null;
        }
    }
    private static void Open()
    {
        string path = Environment.GetEnvironmentVariable("DONDON_CHAT_REVIEW");
        if (string.IsNullOrWhiteSpace(path)) { return; }
        try
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }
            writer = new StreamWriter(path, true, new UTF8Encoding(false)) { AutoFlush = true };
            Application.quitting -= Close; Application.quitting += Close;
        }
        catch (IOException ex) { Debug.LogWarning("DONDON_CHAT_REVIEW 파일을 열지 못했습니다: " + ex.Message); }
        catch (UnauthorizedAccessException ex) { Debug.LogWarning("DONDON_CHAT_REVIEW 파일을 열지 못했습니다: " + ex.Message); }
        catch (ArgumentException ex) { Debug.LogWarning("DONDON_CHAT_REVIEW 경로가 잘못됐습니다: " + ex.Message); }
    }
    private static void Close() { try { writer?.Dispose(); } catch (IOException) { } writer = null; }
    private static string Clean(string value) => string.IsNullOrEmpty(value) ? "" : value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    public static void Write(string time, string room, string speaker, string language, string engine, string key, string text)
    {
        if (writer == null) { return; }
        try { writer.WriteLine(Clean(time) + "\t" + Clean(room) + "\t" + Clean(speaker) + "\t" + Clean(language) + "\t" + Clean(engine) + "\t" + Clean(key) + "\t" + Clean(text)); }
        catch (IOException) { Close(); }
        catch (ObjectDisposedException) { writer = null; }
    }
    /// <summary>묶음 이름의 앞머리로 어느 장치의 말인지 가린다.</summary>
    public static string Engine(string key, string topicEvent)
    {
        if (string.IsNullOrEmpty(key)) { return "social"; }
        if (key.StartsWith("chart.", StringComparison.Ordinal)) { return "chart"; }
        if (key.StartsWith("twitch.", StringComparison.Ordinal)) { return "twitch"; }
        if (key.StartsWith("debate.", StringComparison.Ordinal) || key.StartsWith("talk.debate.", StringComparison.Ordinal)) { return "debate"; }
        if (key.StartsWith("hype.", StringComparison.Ordinal) || key.StartsWith("oddball.", StringComparison.Ordinal)) { return "hype"; }
        if (key.StartsWith("market.", StringComparison.Ordinal)) { return "marketMood"; }
        if (key.StartsWith("mood.", StringComparison.Ordinal)) { return "mood"; }
        if (key.StartsWith("AFTER.", StringComparison.Ordinal)) { return "afterHours"; }
        if (key.StartsWith("talk.", StringComparison.Ordinal)) { return "playerTalk"; }
        return topicEvent == "HYPE" ? "hype" : "social";
    }
}

public sealed partial class InvestorSociety
{
    /// <summary>다음에 종목들의 값을 찍을 시각(사회 시계, 초).</summary>
    private double situationNextSample;

    /// <summary>장중에 2초마다 모든 종목의 값을 찍어 당일 기록(고가·저가, 최근 2분)을 잇는다. 아무도 묻지 않는 종목도 기록은 이어져야 한다.</summary>
    private void TickSituations()
    {
        if (day == null || StockManager.Instance == null || day.Phase != MarketPhase.Trading || ReadMarketDay == null) { return; }
        double now = SocialTime.Seconds;
        // 시계가 뒤로 갔으면(불러오기) 바로 다시 찍는다.
        if (now < situationNextSample && situationNextSample - now < ChatSituationTrack.SampleSec * 2) { return; }
        situationNextSample = now + ChatSituationTrack.SampleSec;
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || !stock.IsListed || stock.IsDelisted || stock.currentPrice <= 0m) { continue; }
            SituationOf(stock);
        }
    }

    /// <summary>이 종목의 지금 상황. 기준가를 알 수 없으면 valid가 false인 빈 값이다(그때는 예전처럼 말한다).</summary>
    public ChatSituationRead SituationOf(Stock stock)
    {
        if (stock == null || stock.currentPrice <= 0m || day == null || ReadMarketDay == null) { return default; }
        var board = ReadMarketDay(stock);
        if (board.reference <= 0m) { return default; }
        return ChatSituationBoard.Observe(stock, day.Day, SocialTime.Seconds, board.reference, board.high, board.low);
    }

    /// <summary>이 봇이 이 종목을 두고 말할 때의 처지. 들고 있지 않으면 구경꾼이다.</summary>
    private static ChatSpeakerView BotView(TradeBot bot, Stock stock, out ChatStance stance)
    {
        decimal cost = bot.GetOwnedVolume(stock) > 0m ? bot.GetOwnedAverage(stock) : 0m;
        stance = ChatSituationMath.Stance(cost, stock.currentPrice, cost > 0m);
        return ChatSituationMath.View(cost, stock.currentPrice, cost > 0m);
    }
    /// <summary>말끝 버릇은 봇마다 정해져 있다. 난수열은 쓰지 않는다.</summary>
    private static uint SpeechHabit(int botId) => CompanyBusiness.Hash("TAIL:" + botId);

    /// <summary>
    /// 가격이 한 계단(±5%) 움직였을 때 MarketDayController가 급등·급락 사건을 만들기 전에 묻는다.
    /// 상황과 어긋나는 움직임(괴물 급등 중의 한 계단 하락, 투매 중의 한 계단 반등, 조용한 종목의 출렁임)이면 사건을 만들지 않는다.
    /// 판정할 수 없을 때는 예전대로 만든다.
    /// </summary>
    public static bool PriceEventMatters(Stock stock, bool rising)
    {
        var society = Instance;
        if (society == null || stock == null || society.day == null || !society.SocialReady) { return true; }
        var read = society.SituationOf(stock);
        return !read.valid || ChatSituationRules.PriceEventMatters(read.kind, rising);
    }

    /// <summary>
    /// 시세가 계기인 말을 지금 해도 되는가. 봇이 말하기로 한 때와 실제로 말하는 때 사이에 상황이 바뀌었을 수 있으므로 말하기 직전에 다시 본다.
    /// 해도 되는 상황이어도, 그 언어권이 그 상황을 얼마나 입에 올리는지에 따라 가만히 있기도 한다(침묵도 반응이다).
    /// 봇의 난수열은 쓰지 않는다.
    /// </summary>
    private bool MarketTriggerFits(TradeBot bot, SocialIntent intent, SocialFacts facts)
    {
        if (facts == null || intent.replyToId != 0) { return true; }
        bool watch = facts.notice == "WATCH_RISE" || facts.notice == "WATCH_FALL";
        bool priceEvent = string.IsNullOrEmpty(facts.notice) && (facts.sourceEvent == "PriceRise" || facts.sourceEvent == "PriceFall");
        if (!watch && !priceEvent) { return true; }
        var stock = FindSocialStock(facts.symbol);
        if (stock == null) { return true; }
        var read = SituationOf(stock);
        if (!read.valid) { return true; }
        bool rising = watch ? facts.notice == "WATCH_RISE" : facts.sourceEvent == "PriceRise";
        bool fits = ChatSituationRules.PriceEventMatters(read.kind, rising);
        var engine = ChatReactionAssets.Current;
        if (fits && engine != null)
        {
            BotView(bot, stock, out var stance);
            double roll = CompanyBusiness.Hash("SIT:" + bot.Social.Id + ":" + facts.symbol + ":" + (long)(SocialTime.Seconds / 20d)) % 1000u / 1000d;
            fits = roll < engine.TalkChance(read.kind, stance);
        }
        if (!fits)
        {
            ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, actor = bot.Social.Id, kind = "MARKET_TRIGGER_MUTED",
                reason = facts.symbol + " " + (watch ? facts.notice : facts.sourceEvent) + " in " + read.kind, value = (double)read.dayPct });
        }
        return fits;
    }

    /// <summary>
    /// 차트 수다·잔움직임이 쓰는 상황 대사 한 줄. 빈칸은 아직 채우지 않은 틀이다(부르는 쪽이 HypeSlots·ChartSlots로 채운다: 그 빈칸들로 채울 수 있는 문장만 고른다).
    /// 그 언어의 엔진이 없거나 할 말이 없으면 null. key에는 고른 묶음의 이름이 담긴다("market.MonsterDip.gain").
    /// </summary>
    private string SituationLine(TradeBot bot, Stock stock, ChatSituationRead read, out string key)
    {
        key = null;
        var engine = ChatReactionAssets.Current;
        if (engine == null || bot == null || stock == null || !read.valid || read.kind == ChatSituation.Quiet) { return null; }
        var view = BotView(bot, stock, out var stance);
        var slots = HypeSlots(stock, 0, 0m);
        string line = engine.Pick(read.kind, view, stance, slots, HypeRandom.Next, text => hypeRecentLines.Contains(text), out key, out _);
        if (line == null) { return null; }
        hypeRecentLines.Enqueue(line);
        while (hypeRecentLines.Count > Hype("crowdRepeatWindow")) { hypeRecentLines.Dequeue(); }
        return engine.Dress(line, read.kind, SpeechHabit(bot.Social.Id), HypeRandom.Next);
    }

    /// <summary>
    /// 대화 틀에서 나온 시세 반응(혼잣말)을 그 언어의 대사로 바꾼다. 바꿨으면 true이고 text가 화면에 뜰 글이다.
    /// 대상: 안 본 사이의 가격 변화(WATCH_RISE·WATCH_FALL), 급등·급락 사건(PriceRise·PriceFall), 그리고 시세를 말할 수 있는 평소의 혼잣말.
    /// 문장은 방향이 아니라 상황 × 말하는 사람의 처지로 고른다. 번역을 거치지 않으므로 번역용 정보(localizedPhrase 등)는 비운다.
    /// 그 언어의 엔진이 없거나 고를 문장이 없으면 false(예전 길로 간다).
    /// covered: 이 말이 그 언어의 엔진이 맡는 종류인가. true인데 false를 돌려줬다면 엔진이 "지금은 시세 얘기를 안 한다"고 정한 것이므로,
    /// 예전 길에서도 번역된 시세 대사(MKT_…)를 쓰지 않고 평소 하던 말을 한다.
    /// </summary>
    private static bool TryNativeMarketLine(InvestorPsychology mind, SocialTemplate template, SocialMessage message, SocialMessage parent, IEnumerable<SocialMessage> recent, out string text, out bool covered)
    {
        text = null; covered = false;
        var facts = message.facts;
        if (facts == null || string.IsNullOrEmpty(facts.situation) || parent != null || message.targetId != 0 || message.replyToId != 0 || message.act != SocialAct.REACT ||
            facts.signal || message.memory || template.variant == "memory" || template.promisedAction != SocialPromisedAction.None)
        { return false; }
        bool watch = (facts.notice == "WATCH_RISE" || facts.notice == "WATCH_FALL") && facts.observationBotId == message.senderId;
        bool priceEvent = string.IsNullOrEmpty(facts.notice) && (facts.sourceEvent == "PriceRise" || facts.sourceEvent == "PriceFall");
        bool idle = string.IsNullOrEmpty(facts.notice) && template.key.Contains(".plain.") && PrefersMarketLine(message);
        if (!watch && !priceEvent && !idle) { return false; }
        // 언어별 대사는 익명 채팅방의 반말이다. 존댓말을 쓰는 봇은 예전 길의 존댓말 대사로 말한다(그 대사도 같은 상황 판정을 따른다).
        if (template.style == "formal") { return false; }
        var engine = ChatReactionAssets.Current;
        if (engine == null || !Enum.TryParse(facts.situation, out ChatSituation kind) || kind == ChatSituation.Quiet || !ChatSituationBoard.TryRead(facts.symbol, out var read)) { return false; }
        covered = true;
        // 말하기로 한 순간에 본 상황으로 말한다(수치는 가장 최근 것).
        read.kind = kind;
        // 처지는 그 봇의 실제 보유와 평가 손익에서 읽는다.
        bool holds = facts.held && facts.profitKind == "평가" && decimal.TryParse(facts.pct, NumberStyles.Number, SocialCulture, out _);
        decimal result = 0m;
        if (holds) { decimal.TryParse(facts.pct, NumberStyles.Number, SocialCulture, out result); }
        var view = !holds ? ChatSpeakerView.Watcher : result >= ChatSituationMath.DeepProfitPct ? ChatSpeakerView.DeepProfit : result >= ChatSituationMath.ProfitPct ? ChatSpeakerView.Profit :
            result > -ChatSituationMath.ProfitPct ? ChatSpeakerView.Even : result > ChatSituationMath.DeepLossPct ? ChatSpeakerView.Loss : ChatSpeakerView.DeepLoss;
        var stance = !holds ? ChatStance.Watch : result >= 0m ? ChatStance.Gain : ChatStance.Loss;
        // 평소의 혼잣말은 그 언어권이 이 상황을 입에 올리는 만큼만 시세를 말한다(아니면 평소 하던 말을 한다).
        if (!watch && !priceEvent && CompanyBusiness.Hash("SITIDLE:" + message.id) % 1000u / 1000d >= engine.TalkChance(kind, stance)) { return false; }

        var slots = new Dictionary<string, string>(StringComparer.Ordinal);
        slots["stockName"] = GameLocale.Text(facts.stockName ?? facts.symbol ?? "");
        string shortName = ChatHypeAssets.ShortName?.Invoke(facts.symbol);
        if (!string.IsNullOrEmpty(shortName)) { slots["stockShort"] = shortName; }
        slots["price"] = facts.price ?? "";
        engine.FillSlots(read, slots);
        string stanceName = kind + "." + ChatSituationMath.StanceKey(stance), deepName = kind + ".deep";
        bool Repeated(string line)
        {
            int at = Array.IndexOf(engine.Lines(stanceName), line);
            string speech = at >= 0 ? "market." + stanceName + "." + at : "market." + deepName + "." + Array.IndexOf(engine.Lines(deepName), line);
            if (mind.RecentSpeechKeys.Contains(speech)) { return true; }
            if (recent != null) { foreach (var previous in recent) { if (previous.debug?.speechKey == speech) { return true; } } }
            return false;
        }
        var random = CosmeticRandom(mind, message, 0x3b9f11c7u);
        string chosen = engine.Pick(kind, view, stance, slots, random.Next, Repeated, out string key, out int number);
        if (chosen == null) { return false; }
        chosen = engine.Dress(chosen, kind, SpeechHabit(mind.SocialId), random.Next);
        // 혼잣말은 여러 종목 얘기가 섞인 방에 뜬다. 종목 이름이 없는 문장 앞에는 이름을 붙인다.
        text = ChatHypeAssets.Fill(NamedLine(chosen), slots);
        message.debug.speechKey = key + "." + number;
        message.debug.numberStyle = "언어별 시세 반응(" + engine.Code + ")";
        message.localizedPhrase = null; message.localizedValues = null; message.localizedSuffix = ""; message.localizedTarget = null;
        return true;
    }

    /// <summary>확인용 기록에 화면에 뜬 한 줄을 적는다(ChatReview가 켜져 있을 때만 부른다).</summary>
    private void WriteChatReview(TradeBot bot, SocialMessage message)
    {
        string time = day == null ? "" : day.Day + "d " + (day.ClockMinute / 60).ToString("00") + ":" + (day.ClockMinute % 60).ToString("00");
        string key = message.templateKey ?? "";
        if (!string.IsNullOrEmpty(message.debug?.speechKey)) { key += " > " + message.debug.speechKey; }
        // 그 말이 다룬 종목의 지금 상황을 같이 적는다. 말과 상황이 어긋나는지 눈으로 볼 수 있게.
        if (message.facts != null && ChatSituationBoard.TryRead(message.facts.symbol, out var read))
        { key += " | " + message.facts.symbol + " " + read.kind + " " + ChatSituationMath.SignedPercent(read.dayPct) + " retrace " + ChatSituationMath.SharePercent(read.retrace); }
        ChatReview.Write(time, message.roomId.ToString(), bot.Social.Nickname, GameLocale.Code, ChatReview.Engine(message.templateKey, message.topicEvent), key, LocalizedChat.Text(message, message.text));
    }
}
