using System;
using System.Collections.Generic;
using UnityEngine;

// 차트 수다. 봇들이 실제 차트를 보는 것처럼 지금 나온 모양을 두고 떠든다.
// 급등 뒤 급락("와 개빠지네"), 반등("찐반인가?"), 가짜 반등, 쌍봉·쌍바닥, 말아올리기, 박스 돌파, 꼬리, 그리고 자기 타점 자랑과 한탄.
//
// 기존 폭주 연출(ChatSocialHype)과 같은 층이다. 화면 표시 전용이고, 방의 최근 대화·목격·감정·기억·난수열 어디에도 들어가지 않는다.
// 그래서 어떤 봇의 매매 판단도 바꾸지 않는다. 재료는 누구나 보는 시세와, 말하는 봇 자신의 체결뿐이다. 진짜 가치는 읽지 않는다.
// 대사는 언어마다 따로 쓴 파일(Chat/chart_talk.ko/en/ja)에서 온다. 서로의 번역이 아니다.

[Serializable] public sealed class ChartTalkGroup { public string key; public string[] texts; }
[Serializable] public sealed class ChartTalkLocale
{
    public int schemaVersion = 1;
    public ChartTalkGroup[] groups;
    [NonSerialized] public Dictionary<string, string[]> index;
}

/// <summary>언어별 차트 수다 대사. 읽기에 실패하면 이 기능만 꺼진다.</summary>
public static class ChartTalkAssets
{
    /// <summary>군중이 떠드는 모양들. 각각 "이름.any"는 꼭 있어야 하고 "이름.held"·"이름.notHeld"는 있으면 쓴다.</summary>
    public static readonly string[] Shapes =
    {
        "wobble", "dump1", "dump2", "roundtrip", "bleed", "plunge", "bounce1", "bounce2", "bounce3", "deadcat", "doubleTop", "doubleBottom",
        "grindUp", "grindDown", "breakout", "breakdown", "wickUp", "wickDown", "dayHigh", "dayLow", "flat"
    };
    /// <summary>자기 체결을 두고 하는 말과, 그 말에 남들이 다는 답.</summary>
    public static readonly string[] Personal = { "sniper", "topTick", "topSell", "bottomSell", "sniperReply", "topTickReply", "topSellReply", "bottomSellReply" };
    private static readonly string[] None = Array.Empty<string>();
    private static Dictionary<string, ChartTalkLocale> locales;
    private static bool failed;
    public static bool Ready
    {
        get
        {
            if (locales == null && !failed)
            {
                try { Reload(); }
                catch (ArgumentException ex) { failed = true; Debug.LogWarning("차트 수다를 끕니다: " + ex.Message); }
            }
            return locales != null;
        }
    }
    public static void Reload()
    {
        var loaded = new Dictionary<string, ChartTalkLocale>(StringComparer.Ordinal);
        foreach (string code in ChatHypeAssets.Languages)
        {
            var asset = Resources.Load<TextAsset>("Chat/chart_talk." + code);
            if (asset == null) { continue; }
            var locale = JsonUtility.FromJson<ChartTalkLocale>(asset.text);
            if (locale == null || locale.schemaVersion != 1 || locale.groups == null) { throw new ArgumentException("Invalid chart_talk." + code); }
            locale.index = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var group in locale.groups)
            {
                if (group == null || string.IsNullOrEmpty(group.key) || group.texts == null || group.texts.Length == 0 || locale.index.ContainsKey(group.key)) { throw new ArgumentException("Invalid chart_talk group: " + code + " " + group?.key); }
                foreach (string text in group.texts) { if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("Empty chart_talk text: " + code + " " + group.key); } }
                locale.index.Add(group.key, group.texts);
            }
            foreach (string shape in Shapes) { if (!locale.index.ContainsKey(shape + ".any")) { throw new ArgumentException("Missing chart_talk lines: " + code + " " + shape); } }
            foreach (string key in Personal) { if (!locale.index.ContainsKey(key)) { throw new ArgumentException("Missing chart_talk lines: " + code + " " + key); } }
            loaded[code] = locale;
        }
        if (loaded.Count == 0) { throw new ArgumentException("Missing chart_talk files"); }
        locales = loaded; failed = false;
    }
    private static ChartTalkLocale Current => locales.TryGetValue(GameLocale.Code, out var locale) ? locale : locales.TryGetValue("ko", out locale) ? locale : null;
    public static string[] Lines(string key)
    {
        var locale = Current;
        if (locale == null) { foreach (var any in locales.Values) { locale = any; break; } }
        return locale != null && locale.index.TryGetValue(key, out var texts) ? texts : None;
    }
}

public sealed partial class InvestorSociety
{
    // ── 수치: 무엇을 "모양"으로 볼 것인가 ──
    // 시세는 2초마다 찍는다(ChartSampleSec). 꺾임(Reversal) 3%를 넘게 되돌려야 고점·저점으로 친다.
    private const double ChartSampleSec = 2, ChartKeepSec = 900;
    private const decimal Reversal = .03m;
    // 급등: 저점에서 고점까지 10% 이상을 5분 안에. 급락: 고점에서 10% 이상. 깊은 급락: 18% 이상. 원위치: 오르기 전 저점의 2% 안.
    private const decimal PumpRise = .10m, DumpDrop = .10m, DeepDrop = .18m, WobbleDrop = .05m, BackToBase = .02m;
    private const double PumpMaxSec = 300, WobbleMaxSec = 45, SuddenSec = 150, PlungeSec = 120;
    // 냉소: 급등이 한창일 때(저점에서 10%, 25%를 넘을 때) 누군가 시큰둥한 한마디를 던진다("ㅎㅎ", "어차피 곧 내려찍을거임").
    // 급등주를 구경하는 사람이면 누구나 할 법한 말만 쓴다. 뭔가 아는 사람처럼 들리는 말(안다는 티, 세력과 엮인 티)은 쓰지 않는다.
    private static readonly decimal[] KnowingStages = { .10m, .25m };
    private const double KnowingChance = .65;
    // 반등: 8% 이상 빠진 뒤 저점에서 4% 이상. 찐반등: 빠진 폭의 절반 이상 회복. 가짜 반등: 회복이 60%에 못 미치고 저점을 다시 깸.
    private const decimal FallForBounce = .08m, BounceRise = .04m, RealBounceShare = .5m, DeadCatShare = .6m;
    // 쌍봉·쌍바닥: 두 고점(저점)이 1.5% 안에서 맞고, 그 봉우리가 앞선 바닥에서 6% 이상 올라온 것이어야 한다.
    private const decimal TwinTolerance = .015m, TwinProminence = .06m;
    // 말아올리기: 150초 동안 7~16% 오르면서 중간에 2.5% 넘게 눌린 적이 없다.
    private const double GrindSec = 150; private const decimal GrindMove = .07m, GrindMax = .16m, GrindPullback = .025m;
    // 박스: 140초 전부터 20초 전까지 폭이 2.5% 이하였다가 지금 그 밖으로 3% 넘게 나갔다.
    private const double BoxFromSec = 140, BoxToSec = 20; private const decimal BoxRange = .025m, BoxEscape = .03m;
    // 꼬리: 30초 사이에 5% 넘게 튀었다가 제자리(±1.5%)로 돌아왔다.
    private const double WickSec = 30; private const decimal WickSpike = .05m, WickBack = .015m;
    // 당일 등락: 전일 대비 +15%, +22%를 넘으며 신고가를 찍을 때(내릴 때도 같다).
    private static readonly decimal[] DayStages = { .15m, .22m };
    // 횡보: 5분 동안 폭이 1% 이하.
    private const double FlatSec = 300, FlatCheckSec = 60, FlatRestSec = 420; private const decimal FlatRange = .01m; private const double FlatChance = .3;
    // 타점: 체결 묶음(30초 안의 연속 체결)의 평균가가 그 무렵(±40초) 고점·저점의 3.5% 안이고, 그 뒤 값이 8%(자랑)·10%(한탄) 넘게 갔을 때.
    private const double HandClusterSec = 30, HandNearSec = 40, HandLookbackSec = 180, HandExpireSec = 360;
    private const decimal HandNear = .035m, HandPriorMove = .06m, SniperGain = .08m, RegretMove = .10m;
    // 말하는 속도: 차트 수다 한 줄 사이 최소 간격, 같은 종목 같은 모양의 쉬는 시간.
    private const double ChartLineGapMin = 1.1, ChartLineGapMax = 2.6, ChartShapeRestSec = 150, ChartWindowRestSec = 280;
    // 값이 한 번 움직이면 조건에 맞는 봇이 수십 명씩 나온다. 그중 한 명만 말한다: 같은 종목의 같은 얘기는 2분에 한 번, 전체로도 7~15초에 한 번.
    private const double PersonalRestSec = 120, PersonalGapMin = 7, PersonalGapMax = 15;

    private struct ChartPivot { public decimal price; public double at; public bool high; }
    private sealed class ChartTrack
    {
        public readonly List<(double at, decimal price)> samples = new(512);
        public readonly List<ChartPivot> pivots = new(12);
        public readonly Dictionary<string, double> rest = new(StringComparer.Ordinal);
        public bool legUp = true; public decimal legExtreme; public double legAt;
        /// <summary>마지막 고점(저점)에 대해 이미 말한 단계.</summary>
        public int dumpStage, bounceStage, knowingStage; public bool twinTop, twinBottom, deadCat, roundTrip;
        public int dayUpStage, dayDownStage, day = -1;
    }
    private sealed class ChartBurst { public Stock stock; public string key; public int left, spoken; public double next; public decimal peak, low; public long replyTo; public int skipBot; public string replyName; }
    /// <summary>차트 수다가 마지막으로 다룬 종목. 화제가 다른 종목으로 넘어가면 첫 마디에 종목 이름을 붙인다.</summary>
    private Stock chartLastStock;
    /// <summary>종목 이름이 없는 대사 앞에 이름을 붙인다("벽 믿고 버틴다" → "테줌마 벽 믿고 버틴다"). 여러 종목 얘기가 섞인 방에서 무엇을 두고 하는 말인지 알 수 있게.</summary>
    private static string NamedLine(string text) => text == null || text.Contains("{stockName}") || text.Contains("{fromName}") ? text : "{stockName} " + text;
    /// <summary>한 봇이 한 종목에서 최근에 사고판 묶음. 말하는 봇 자신의 체결만 본다.</summary>
    private sealed class ChartHand
    {
        public TradeBot bot; public Stock stock;
        public decimal buyValue, buyVolume, sellValue, sellVolume; public double buyFirst, buyLast = double.MinValue, sellFirst, sellLast = double.MinValue;
        public bool buySpoken, sellSpoken;
        public decimal BuyPrice => buyVolume > 0m ? buyValue / buyVolume : 0m;
        public decimal SellPrice => sellVolume > 0m ? sellValue / sellVolume : 0m;
    }

    private readonly Dictionary<Stock, ChartTrack> chartTracks = new();
    private readonly List<ChartBurst> chartBursts = new();
    private readonly Dictionary<(int bot, Stock stock), ChartHand> chartHands = new();
    private readonly List<(int bot, Stock stock)> chartHandKeys = new();
    private double chartNextSample, chartNextLine, chartTickAt = double.NaN, chartFlatCheck, chartFlatRest, chartPersonalNext;
    private readonly Dictionary<(Stock stock, string key), double> chartPersonalRest = new();
    /// <summary>확인용: 지금까지 알아챈 모양과 한 말의 수.</summary>
    public int ChartShapesSeen { get; private set; }
    public int ChartLinesSaid { get; private set; }
    public readonly Queue<string> ChartTrace = new(64);
    private void TraceChart(string line)
    {
        ChartTrace.Enqueue(line);
        while (ChartTrace.Count > 60) { ChartTrace.Dequeue(); }
    }

    private void TickChartTalk()
    {
        if (!HypeAllowed || day == null || !SocialReady || StockManager.Instance == null || !ChatHypeAssets.Ready || !ChartTalkAssets.Ready) { return; }
        double now = SocialTime.Seconds;
        if (Math.Abs(now - chartTickAt) > 30) { chartTracks.Clear(); chartBursts.Clear(); chartHands.Clear(); chartPersonalRest.Clear(); chartNextSample = 0; chartPersonalNext = 0; }
        chartTickAt = now;
        if (day.Phase == MarketPhase.Trading && now >= chartNextSample)
        {
            chartNextSample = now + ChartSampleSec;
            foreach (var stock in StockManager.Instance.stocks)
            {
                if (stock == null || !stock.IsListed || stock.IsDelisted || !stock.AcceptsOrders || stock.currentPrice <= 0m) { continue; }
                if (!chartTracks.TryGetValue(stock, out var track)) { chartTracks.Add(stock, track = new ChartTrack()); }
                if (track.day != day.Day) { ResetTrack(track); track.day = day.Day; }
                FeedChart(track, stock.currentPrice, now);
                ReadShapes(track, stock, stock.currentPrice, now);
            }
            ReadHands(now);
            WatchFlat(now);
        }
        SpeakChart(now);
    }
    private static void ResetTrack(ChartTrack track)
    {
        track.samples.Clear(); track.pivots.Clear(); track.rest.Clear(); track.legExtreme = 0m; track.legUp = true;
        track.dumpStage = track.bounceStage = track.knowingStage = track.dayUpStage = track.dayDownStage = 0; track.twinTop = track.twinBottom = track.deadCat = track.roundTrip = false;
    }

    // ── 차트 읽기 ──
    /// <summary>값을 하나 찍고, 3% 넘게 되돌렸으면 직전 끝을 고점(저점)으로 굳힌다.</summary>
    private static void FeedChart(ChartTrack track, decimal price, double now)
    {
        track.samples.Add((now, price));
        while (track.samples.Count > 0 && now - track.samples[0].at > ChartKeepSec) { track.samples.RemoveAt(0); }
        if (track.legExtreme <= 0m)
        {
            track.legExtreme = price; track.legAt = now; track.legUp = true;
            track.pivots.Add(new ChartPivot { price = price, at = now, high = false });
            return;
        }
        if (track.legUp)
        {
            if (price > track.legExtreme) { track.legExtreme = price; track.legAt = now; }
            else if (price <= track.legExtreme * (1m - Reversal))
            {
                AddPivot(track, new ChartPivot { price = track.legExtreme, at = track.legAt, high = true });
                track.legUp = false; track.legExtreme = price; track.legAt = now; track.dumpStage = 0; track.twinTop = false; track.deadCat = false; track.roundTrip = false;
            }
        }
        else
        {
            if (price < track.legExtreme) { track.legExtreme = price; track.legAt = now; }
            else if (price >= track.legExtreme * (1m + Reversal))
            {
                AddPivot(track, new ChartPivot { price = track.legExtreme, at = track.legAt, high = false });
                track.legUp = true; track.legExtreme = price; track.legAt = now; track.bounceStage = 0; track.knowingStage = 0; track.twinBottom = false;
            }
        }
    }
    private static void AddPivot(ChartTrack track, ChartPivot pivot)
    {
        // 첫 점은 임시 저점이다. 첫 꺾임이 위로 났다면(고점이 굳었다면) 그대로 두고, 아래로 났다면 그 점을 고점으로 바꿔 둔다.
        if (track.pivots.Count == 1 && track.pivots[0].high == pivot.high) { track.pivots[0] = pivot; return; }
        track.pivots.Add(pivot);
        if (track.pivots.Count > 10) { track.pivots.RemoveAt(0); }
    }
    private static bool PriceAgo(ChartTrack track, double now, double seconds, out decimal price)
    {
        price = 0m;
        if (track.samples.Count == 0 || now - track.samples[0].at < seconds) { return false; }
        for (int i = track.samples.Count - 1; i >= 0; i--) { if (now - track.samples[i].at >= seconds) { price = track.samples[i].price; return true; } }
        return false;
    }
    /// <summary>from~to(초, 절대 시각) 사이의 최저·최고와 그 시각. 찍힌 값이 없으면 false.</summary>
    private static bool ChartRange(ChartTrack track, double from, double to, out decimal low, out decimal high, out double lowAt, out double highAt)
    {
        low = high = 0m; lowAt = highAt = 0;
        foreach (var sample in track.samples)
        {
            if (sample.at < from || sample.at > to) { continue; }
            if (low == 0m || sample.price < low) { low = sample.price; lowAt = sample.at; }
            if (sample.price > high) { high = sample.price; highAt = sample.at; }
        }
        return low > 0m;
    }

    private void ReadShapes(ChartTrack track, Stock stock, decimal price, double now)
    {
        var pivots = track.pivots; int count = pivots.Count;
        if (count == 0) { return; }
        var last = pivots[count - 1];
        if (!track.legUp && last.high)
        {
            // 고점을 찍고 내려오는 중.
            decimal drop = 1m - price / last.price; double since = now - last.at;
            bool hasBase = count >= 2 && !pivots[count - 2].high; decimal floor = hasBase ? pivots[count - 2].price : 0m;
            bool pumped = hasBase && last.price >= floor * (1m + PumpRise) && last.at - pivots[count - 2].at <= PumpMaxSec;
            if (pumped)
            {
                if (track.dumpStage < 1 && drop >= WobbleDrop && drop < DumpDrop && since <= WobbleMaxSec) { track.dumpStage = 1; Shape(track, stock, "wobble", now, 2, last.price, floor); }
                if (track.dumpStage < 2 && drop >= DumpDrop) { track.dumpStage = 2; Shape(track, stock, since <= SuddenSec ? "dump1" : "bleed", now, since <= SuddenSec ? 4 : 3, last.price, floor); }
                if (track.dumpStage < 3 && drop >= DeepDrop) { track.dumpStage = 3; Shape(track, stock, "dump2", now, 4, last.price, floor); }
                if (!track.roundTrip && track.dumpStage >= 2 && price <= floor * (1m + BackToBase)) { track.roundTrip = true; Shape(track, stock, "roundtrip", now, 3, last.price, floor); }
            }
            else
            {
                if (track.dumpStage < 2 && drop >= DumpDrop && since <= PlungeSec) { track.dumpStage = 2; Shape(track, stock, "plunge", now, 4, last.price, price); }
                if (track.dumpStage < 3 && track.dumpStage >= 2 && drop >= DeepDrop) { track.dumpStage = 3; Shape(track, stock, "dump2", now, 3, last.price, price); }
            }
            if (count >= 3 && pivots[count - 3].high)
            {
                var firstTop = pivots[count - 3]; var dip = pivots[count - 2];
                decimal before = count >= 4 ? pivots[count - 4].price : track.samples[0].price;
                // 쌍봉: 앞 고점과 거의 같은 높이에서 또 꺾였다.
                if (!track.twinTop && Math.Abs(last.price / firstTop.price - 1m) <= TwinTolerance && firstTop.price >= before * (1m + TwinProminence))
                { track.twinTop = true; Shape(track, stock, "doubleTop", now, 2, Math.Max(last.price, firstTop.price), dip.price); }
                // 가짜 반등: 크게 빠졌다가 조금 되돌린 뒤, 앞 저점을 다시 깼다.
                decimal fall = firstTop.price - dip.price;
                if (!track.deadCat && fall >= firstTop.price * FallForBounce && last.price >= dip.price * (1m + BounceRise) && last.price - dip.price < fall * DeadCatShare && price < dip.price)
                { track.deadCat = true; Shape(track, stock, "deadcat", now, 3, last.price, price); }
            }
        }
        else if (track.legUp && !last.high)
        {
            // 저점을 찍고 올라오는 중.
            bool hasTop = count >= 2 && pivots[count - 2].high; decimal top = hasTop ? pivots[count - 2].price : 0m;
            if (hasTop && top - last.price >= top * FallForBounce)
            {
                decimal recovered = (price - last.price) / (top - last.price);
                if (track.bounceStage < 1 && price >= last.price * (1m + BounceRise)) { track.bounceStage = 1; Shape(track, stock, "bounce1", now, 2, top, last.price); }
                if (track.bounceStage < 2 && recovered >= RealBounceShare) { track.bounceStage = 2; Shape(track, stock, "bounce2", now, 3, top, last.price); }
                if (track.bounceStage < 3 && price > top) { track.bounceStage = 3; Shape(track, stock, "bounce3", now, 3, top, last.price); }
            }
            if (count >= 3 && !pivots[count - 3].high)
            {
                var firstLow = pivots[count - 3];
                decimal before = count >= 4 ? pivots[count - 4].price : track.samples[0].price;
                if (!track.twinBottom && Math.Abs(last.price / firstLow.price - 1m) <= TwinTolerance && firstLow.price <= before * (1m - TwinProminence) && price >= last.price * (1m + BounceRise))
                { track.twinBottom = true; Shape(track, stock, "doubleBottom", now, 2, pivots[count - 2].price, Math.Min(last.price, firstLow.price)); }
            }
        }

        // 급등이 한창이다: 누군가 뜻 모를 한마디를 던진다. 한 번 오를 때 단계마다 한 번, 그것도 매번은 아니다.
        if (track.legUp && track.knowingStage < KnowingStages.Length && ChartRange(track, now - PumpMaxSec, now, out decimal pumpLow, out _, out _, out _) &&
            price >= pumpLow * (1m + KnowingStages[track.knowingStage]))
        {
            track.knowingStage++;
            if (stock == null || HypeRandom.Next() < KnowingChance) { Shape(track, stock, "knowing", now, track.knowingStage, price, pumpLow, 0); }
        }

        // 일정 시간 창으로 보는 모양들.
        if (PriceAgo(track, now, GrindSec, out decimal start) && ChartRange(track, now - GrindSec, now, out decimal low, out decimal high, out _, out _))
        {
            decimal move = price / start - 1m;
            if (Math.Abs(move) >= GrindMove && Math.Abs(move) <= GrindMax)
            {
                // 가는 방향의 반대로 가장 크게 되돌린 폭.
                decimal worst = 0m, extreme = start; bool up = move > 0m;
                foreach (var sample in track.samples)
                {
                    if (sample.at < now - GrindSec) { continue; }
                    if (up) { if (sample.price > extreme) { extreme = sample.price; } worst = Math.Max(worst, 1m - sample.price / extreme); }
                    else { if (sample.price < extreme) { extreme = sample.price; } worst = Math.Max(worst, sample.price / extreme - 1m); }
                }
                if (worst < GrindPullback) { Shape(track, stock, up ? "grindUp" : "grindDown", now, 2, high, low, ChartWindowRestSec); }
            }
        }
        if (ChartRange(track, now - BoxFromSec, now - BoxToSec, out decimal boxLow, out decimal boxHigh, out double boxLowAt, out double boxHighAt) &&
            now - track.samples[0].at >= BoxFromSec && boxHigh <= boxLow * (1m + BoxRange))
        {
            if (price >= boxHigh * (1m + BoxEscape)) { Shape(track, stock, "breakout", now, 3, boxHigh, boxLow, ChartWindowRestSec); }
            else if (price <= boxLow * (1m - BoxEscape)) { Shape(track, stock, "breakdown", now, 3, boxHigh, boxLow, ChartWindowRestSec); }
        }
        if (PriceAgo(track, now, WickSec, out decimal root) && ChartRange(track, now - WickSec, now, out decimal dipLow, out decimal spikeHigh, out _, out _) && Math.Abs(price / root - 1m) <= WickBack)
        {
            if (spikeHigh >= root * (1m + WickSpike)) { Shape(track, stock, "wickUp", now, 2, spikeHigh, price, ChartWindowRestSec); }
            else if (dipLow <= root * (1m - WickSpike)) { Shape(track, stock, "wickDown", now, 2, price, dipLow, ChartWindowRestSec); }
        }
        if (stock != null)
        {
            // 호가창에 큰 벽이 서 있으면 그 얘기를 한다.
            var book = ManipulationTools.Walls(stock);
            if (book.Bid >= .5f && book.Bid > book.Ask) { Shape(track, stock, "bidWall", now, 2, price, book.BidPrice, ChartWindowRestSec); }
            else if (book.Ask >= .5f && book.Ask > book.Bid) { Shape(track, stock, "askWall", now, 2, book.AskPrice, price, ChartWindowRestSec); }
        }
        if (ReadMarketDay != null && stock != null)
        {
            var board = ReadMarketDay(stock);
            if (board.reference > 0m && ChartRange(track, 0, now, out decimal sessionLow, out decimal sessionHigh, out _, out _))
            {
                decimal dayMove = price / board.reference - 1m;
                if (track.dayUpStage < DayStages.Length && dayMove >= DayStages[track.dayUpStage] && price >= sessionHigh)
                { track.dayUpStage++; Shape(track, stock, "dayHigh", now, 2, price, sessionLow, 0); }
                if (track.dayDownStage < DayStages.Length && dayMove <= -DayStages[track.dayDownStage] && price <= sessionLow)
                { track.dayDownStage++; Shape(track, stock, "dayLow", now, 2, sessionHigh, price, 0); }
            }
        }
    }
    /// <summary>아무 일도 없는 차트. 다른 얘깃거리가 없을 때만 가끔 한 줄.</summary>
    private void WatchFlat(double now)
    {
        if (now < chartFlatCheck) { return; }
        chartFlatCheck = now + FlatCheckSec;
        if (now < chartFlatRest || chartBursts.Count > 0 || hypeRun != null || HypeRandom.Next() >= FlatChance) { return; }
        Stock chosen = null; ChartTrack chosenTrack = null; int seen = 0;
        foreach (var pair in chartTracks)
        {
            if (!pair.Key.AcceptsOrders || pair.Value.samples.Count == 0 || now - pair.Value.samples[0].at < FlatSec) { continue; }
            if (ChartRange(pair.Value, now - FlatSec, now, out decimal low, out decimal high, out _, out _) && high <= low * (1m + FlatRange) && HypeRandom.Range(0, ++seen) == 0)
            { chosen = pair.Key; chosenTrack = pair.Value; }
        }
        if (chosen == null) { return; }
        chartFlatRest = now + FlatRestSec;
        Shape(chosenTrack, chosen, "flat", now, 1, chosen.currentPrice, chosen.currentPrice, 0);
    }

    /// <summary>모양 하나를 알아챘다. 같은 종목의 같은 모양은 한동안 다시 말하지 않는다.</summary>
    private void Shape(ChartTrack track, Stock stock, string key, double now, int lines, decimal peak, decimal low, double restSec = ChartShapeRestSec)
    {
        if (track.rest.TryGetValue(key, out double until) && now < until) { return; }
        track.rest[key] = now + restSec;
        // 종목 없이 부르면 점검용이다(ChartSelfTest). 알아챈 이름만 적고 아무 말도 하지 않는다.
        if (stock == null) { chartDryShapes.Add(key); return; }
        ChartShapesSeen++;
        TraceChart(day.ClockMinute / 60 + ":" + (day.ClockMinute % 60).ToString("00") + " " + stock.symbol + " " + key + " price " + Money(stock.currentPrice) + " peak " + Money(peak) + " low " + Money(low));
        ChatSocialAudit.Journal.Event(new SocialEvent { time = SocialTime.GameMinute, kind = "CHART_SHAPE", reason = stock.symbol + " " + key, value = (double)stock.currentPrice });
        // 같은 종목에 대해 아직 덜 한 얘기는 새 모양이 덮는다(이미 지나간 얘기를 뒤늦게 하지 않는다). 폭주 연출이 그 종목을 다루는 중이면 줄 수를 줄인다.
        chartBursts.RemoveAll(burst => burst.stock == stock && burst.replyTo == 0);
        if (hypeRun != null && hypeRun.stock == stock) { lines = Math.Max(1, lines - 2); }
        chartBursts.Add(new ChartBurst { stock = stock, key = key, left = lines, next = now + .4 + HypeRandom.Next() * 1.2, peak = peak, low = low });
        while (chartBursts.Count > 3) { chartBursts.RemoveAt(0); }
    }

    // ── 타점: 말하는 봇 자신의 체결 ──
    private void ObserveChartFill(Stock stock, decimal price, decimal volume, IOrderOwner buyer, IOrderOwner seller)
    {
        if (stock == null || price <= 0m || volume <= 0m || day == null) { return; }
        if (PlayerAccounts.IsPlayer(buyer) != PlayerAccounts.IsPlayer(seller)) { Sfx.Play("OrderFill", .7f, null, .03f, .3f); }
        if (!HypeAllowed) { return; }
        double now = SessionClock.Time;
        if (buyer is TradeBot buying && buying.Social != null)
        {
            var hand = Hand(buying, stock);
            if (now - hand.buyLast > HandClusterSec) { hand.buyValue = hand.buyVolume = 0m; hand.buyFirst = now; hand.buySpoken = false; }
            hand.buyValue += price * volume; hand.buyVolume += volume; hand.buyLast = now;
        }
        if (seller is TradeBot selling && selling.Social != null)
        {
            var hand = Hand(selling, stock);
            if (now - hand.sellLast > HandClusterSec) { hand.sellValue = hand.sellVolume = 0m; hand.sellFirst = now; hand.sellSpoken = false; }
            hand.sellValue += price * volume; hand.sellVolume += volume; hand.sellLast = now;
        }
    }
    private ChartHand Hand(TradeBot bot, Stock stock)
    {
        var key = (bot.Social.Id, stock);
        if (!chartHands.TryGetValue(key, out var hand)) { chartHands.Add(key, hand = new ChartHand { bot = bot, stock = stock }); }
        return hand;
    }
    private void ReadHands(double now)
    {
        chartHandKeys.Clear();
        foreach (var pair in chartHands)
        {
            var hand = pair.Value;
            if (now - hand.buyLast > HandExpireSec && now - hand.sellLast > HandExpireSec || hand.bot == null) { chartHandKeys.Add(pair.Key); continue; }
            if (!chartTracks.TryGetValue(hand.stock, out var track) || !hand.stock.AcceptsOrders) { continue; }
            decimal price = hand.stock.currentPrice;
            // 산 묶음: 마지막 체결에서 6초는 지나야(다 산 뒤에) 평가한다.
            if (!hand.buySpoken && hand.buyVolume > 0m && now - hand.buyLast >= 6 && now - hand.buyLast <= HandExpireSec && hand.bot.GetOwnedVolume(hand.stock) > 0m &&
                ChartRange(track, hand.buyFirst - HandNearSec, hand.buyLast + HandNearSec, out decimal low, out decimal high, out _, out _) &&
                ChartRange(track, hand.buyFirst - HandLookbackSec, hand.buyFirst, out decimal priorLow, out decimal priorHigh, out _, out _))
            {
                decimal entry = hand.BuyPrice;
                // 저점 근처에서 줍고(그 전에 6% 넘게 빠져 있었고) 그 뒤 8% 넘게 올랐다: 타점 자랑.
                if (entry <= low * (1m + HandNear) && priorHigh >= low * (1m + HandPriorMove) && price >= entry * (1m + SniperGain))
                { hand.buySpoken = true; Personal(hand, "sniper", entry, price, now, .3 + hand.bot.Social.Psychology.Traits.Bravado / 100d * .65); }
                // 고점 근처에서 타고(그 전에 6% 넘게 올라 있었고) 그 뒤 10% 넘게 빠졌다: 왜 내가 타면 떨어지냐.
                else if (entry >= high * (1m - HandNear) && priorLow <= high * (1m - HandPriorMove) && price <= entry * (1m - RegretMove))
                { hand.buySpoken = true; Personal(hand, "topTick", entry, price, now, .7); }
            }
            if (!hand.sellSpoken && hand.sellVolume > 0m && now - hand.sellLast >= 6 && now - hand.sellLast <= HandExpireSec &&
                ChartRange(track, hand.sellFirst - HandNearSec, hand.sellLast + HandNearSec, out decimal sellLow, out decimal sellHigh, out _, out _) &&
                ChartRange(track, hand.sellFirst - HandLookbackSec, hand.sellFirst, out decimal beforeLow, out decimal beforeHigh, out _, out _))
            {
                decimal exit = hand.SellPrice;
                // 고점 근처에서 팔았고 그 뒤 10% 넘게 빠졌다: 고점 매도 자랑.
                if (exit >= sellHigh * (1m - HandNear) && beforeLow <= sellHigh * (1m - HandPriorMove) && price <= exit * (1m - RegretMove))
                { hand.sellSpoken = true; Personal(hand, "topSell", exit, price, now, .3 + hand.bot.Social.Psychology.Traits.Bravado / 100d * .65); }
                // 저점 근처에서 던졌고 그 뒤 8% 넘게 올랐다: 내가 팔면 오르네.
                else if (exit <= sellLow * (1m + HandNear) && beforeHigh >= sellLow * (1m + HandPriorMove) && price >= exit * (1m + SniperGain))
                { hand.sellSpoken = true; Personal(hand, "bottomSell", exit, price, now, .7); }
            }
        }
        foreach (var key in chartHandKeys) { chartHands.Remove(key); }
    }
    private void Personal(ChartHand hand, string key, decimal mine, decimal price, double now, double chance)
    {
        var bot = hand.bot;
        // 방금 누가 같은 얘기를 했으면 이 봇은 속으로만 생각한다.
        if (now < chartPersonalNext || chartPersonalRest.TryGetValue((hand.stock, key), out double rest) && now < rest) { return; }
        ChartShapesSeen++;
        bool speaks = bot.isActiveAndEnabled && Present(bot, 0) && !IsTyping(bot) && bot != ResolveOddball() && HypeRandom.Next() < chance
            && bot.Social.Psychology.Traits.Politeness < Config.Int("styleFormalMin");
        TraceChart(day.ClockMinute / 60 + ":" + (day.ClockMinute % 60).ToString("00") + " " + hand.stock.symbol + " " + key + " " + bot.Social.Nickname + " mine " + Money(mine) + " now " + Money(price) + (speaks ? "" : " (silent)"));
        if (!speaks) { return; }
        string text = NamedLine(PickHypeText(ChartTalkAssets.Lines(key), Array.Empty<string>()));
        if (text == null) { return; }
        chartLastStock = hand.stock;
        var slots = ChartSlots(hand.stock, price, price, mine);
        var said = PublishHype(bot, ChatHypeAssets.Fill(text, slots), "chart." + key, hand.stock, 0, 1);
        hypeSpokeAt[bot.Social.Id] = now; ChartLinesSaid++;
        chartPersonalNext = now + PersonalGapMin + HypeRandom.Next() * (PersonalGapMax - PersonalGapMin); chartPersonalRest[(hand.stock, key)] = now + PersonalRestSec;
        TraceChart("    > " + bot.Social.Nickname + ": " + said.last.text + "  [" + key + "]");
        chartNextLine = Math.Max(chartNextLine, said.doneAt + ChartLineGapMin);
        // 자랑과 한탄에는 한두 명이 답을 단다.
        chartBursts.Add(new ChartBurst { stock = hand.stock, key = key + "Reply", left = HypeRandom.Next() < .5 ? 2 : 1, next = said.doneAt + 1.6 + HypeRandom.Next() * 1.8, peak = price, low = price, replyTo = said.last.id, skipBot = bot.Social.Id, replyName = bot.Social.Nickname });
    }

    // ── 갈아타기 ──
    private double rotationRest;
    /// <summary>봇이 가진 종목을 던지고 달아오른 종목으로 갈아탔다(TradeBot.TryChase). 가끔 그 얘기를 채팅에 한다. 표시만 한다.</summary>
    public void NoteRotation(TradeBot bot, Stock from, Stock into)
    {
        if (!HypeAllowed || day == null || !SocialReady || bot == null || bot.Social == null || !ChatHypeAssets.Ready || !ChartTalkAssets.Ready) { return; }
        double now = SocialTime.Seconds;
        TraceChart(day.ClockMinute / 60 + ":" + (day.ClockMinute % 60).ToString("00") + " " + into.symbol + " rotate " + bot.Social.Nickname + " sold " + from.symbol);
        if (now < rotationRest || !bot.isActiveAndEnabled || !Present(bot, 0) || IsTyping(bot) || bot.Social.Psychology.Traits.Politeness >= Config.Int("styleFormalMin")) { return; }
        string text = PickHypeText(ChartTalkAssets.Lines("rotate"), Array.Empty<string>());
        if (text == null) { return; }
        rotationRest = now + 18 + HypeRandom.Next() * 14;
        var slots = ChartSlots(into, into.currentPrice, into.currentPrice, 0m);
        slots["fromName"] = GameLocale.Text(from.stockName ?? from.symbol ?? "");
        var said = PublishHype(bot, ChatHypeAssets.Fill(text, slots), "chart.rotate", into, 0, 1);
        hypeSpokeAt[bot.Social.Id] = now; ChartLinesSaid++;
        TraceChart("    > " + bot.Social.Nickname + ": " + said.last.text + "  [rotate]");
    }

    // ── 점검 ──
    private readonly List<string> chartDryShapes = new();
    /// <summary>
    /// 지어낸 가격 흐름을 넣어 보고 어떤 모양을 알아채는지 돌려준다. 시세·봇·채팅에는 아무것도 하지 않는다(말하지 않는다).
    /// path는 (그 값까지 가는 데 걸리는 초, 값)의 나열이고, 사이는 곧게 잇는다.
    /// </summary>
    public string ChartSelfTest(params (double seconds, decimal price)[] path)
    {
        var track = new ChartTrack(); chartDryShapes.Clear();
        double now = 0; decimal price = path[0].price;
        FeedChart(track, price, now);
        for (int i = 1; i < path.Length; i++)
        {
            decimal from = price; int steps = Math.Max(1, (int)Math.Round(path[i].seconds / ChartSampleSec));
            for (int step = 1; step <= steps; step++)
            {
                now += ChartSampleSec; price = from + (path[i].price - from) * step / steps;
                FeedChart(track, price, now); ReadShapes(track, null, price, now);
            }
        }
        return string.Join(" > ", chartDryShapes);
    }

    // ── 말하기 ──
    private void SpeakChart(double now)
    {
        if (chartBursts.Count == 0 || now < chartNextLine || now < hypeQuietUntil) { return; }
        for (int i = 0; i < chartBursts.Count; i++)
        {
            var burst = chartBursts[i];
            if (now < burst.next) { continue; }
            // 얘기가 너무 늦어졌으면 버린다. 차트는 이미 다른 모양이다.
            if (now - burst.next > 25 || !burst.stock.AcceptsOrders) { chartBursts.RemoveAt(i); return; }
            var bot = PickHypeSpeaker(now);
            if (bot == null || bot.Social.Id == burst.skipBot) { burst.next = now + 1; return; }
            // 존댓말을 쓰는 봇은 이런 수다에 끼지 않는다. 익명 채팅방의 차트 수다는 반말이다.
            if (bot.Social.Psychology.Traits.Politeness >= Config.Int("styleFormalMin")) { burst.next = now + .3; return; }
            bool reply = burst.replyTo != 0;
            string text = reply ? PickHypeText(ChartTalkAssets.Lines(burst.key), Array.Empty<string>())
                : PickHypeText(ChartTalkAssets.Lines(burst.key + ".any"), ChartTalkAssets.Lines(burst.key + (bot.GetOwnedVolume(burst.stock) > 0m ? ".held" : ".notHeld")));
            // 답글은 누구에게 하는 말인지 붙인다. 새 화제의 첫 마디, 다른 종목 얘기 뒤의 첫 마디에는 종목 이름을 붙인다.
            if (text != null && reply && !string.IsNullOrEmpty(burst.replyName)) { text = "@" + burst.replyName + " " + text; }
            else if (text != null && !reply && (burst.spoken == 0 || chartLastStock != burst.stock)) { text = NamedLine(text); }
            if (text != null)
            {
                burst.spoken++; if (!reply) { chartLastStock = burst.stock; }
                var said = PublishHype(bot, ChatHypeAssets.Fill(text, ChartSlots(burst.stock, burst.peak, burst.low, 0m)), "chart." + burst.key, burst.stock, burst.replyTo, 0);
                hypeSpokeAt[bot.Social.Id] = now; ChartLinesSaid++;
                TraceChart("    > " + bot.Social.Nickname + ": " + said.last.text + "  [" + burst.key + (reply ? "" : bot.GetOwnedVolume(burst.stock) > 0m ? ", 보유" : ", 미보유") + "]");
                chartNextLine = said.doneAt + ChartLineGapMin + HypeRandom.Next() * (ChartLineGapMax - ChartLineGapMin);
            }
            burst.left--; burst.next = chartNextLine;
            if (burst.left <= 0 || text == null) { chartBursts.RemoveAt(i); }
            return;
        }
    }
    /// <summary>{stockName} {price} {peak} {low} {drop}(고점 대비 %) {rise}(저점 대비 %) {entry}(내 체결가) {gain}(내 체결가 대비 %) {dayPct}.</summary>
    private Dictionary<string, string> ChartSlots(Stock stock, decimal peak, decimal low, decimal mine)
    {
        var slots = HypeSlots(stock, 0, 0m);
        decimal price = stock.currentPrice;
        slots["peak"] = Money(peak); slots["low"] = Money(low);
        slots["drop"] = peak > 0m ? Math.Abs((price / peak - 1m) * 100m).ToString("0", SocialCulture) : "0";
        slots["rise"] = low > 0m ? Math.Abs((price / low - 1m) * 100m).ToString("0", SocialCulture) : "0";
        slots["entry"] = Money(mine);
        slots["gain"] = mine > 0m ? Math.Abs((price / mine - 1m) * 100m).ToString("0", SocialCulture) : "0";
        decimal dayMove = 0m;
        if (ReadMarketDay != null) { var board = ReadMarketDay(stock); if (board.reference > 0m) { dayMove = (price / board.reference - 1m) * 100m; } }
        slots["dayPct"] = dayMove.ToString("+0;-0;0", SocialCulture);
        return slots;
    }
}
