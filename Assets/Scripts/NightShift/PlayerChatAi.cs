using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 새벽 관찰실 AI가 기억하는 것: 예약한 명령, 가격 알림, 채점을 기다리는 분석, 작전 도구가 남긴 흔적, 오늘의 성적.
/// 저장하지 않는다(판을 불러오면 알림과 성적은 비어 있다). 등급(Tier)만 PlayerProgression이 저장한다.
/// 장면이 바뀌어 채팅 엔진이 새로 생기면 통째로 비운다(Reset).
/// </summary>
public static class DawnAi
{
    public const int Slam = 1, Ignite = 2, Nip = 3, WallUp = 4, WallPulled = 5, Wash = 6;
    /// <summary>정밀 분석으로 올리는 값(한 번).</summary>
    public const long UpgradeCost = 5_000_000;
    /// <summary>분석을 올린 뒤 이만큼(초) 지나 채점한다. 매수·매도 의견만, 그 방향으로 1% 넘게 갔으면 적중, 반대로 1% 넘게 갔으면 빗나감.</summary>
    public const float GradeSeconds = 120f; public const decimal GradeMove = .01m;
    /// <summary>작전 흔적을 이만큼(초) 동안 본다.</summary>
    public const float TraceSeconds = 120f;
    public sealed class Job { public double due; public int room; public string command, argument; public Stock stock, other; public long replyTo; public bool byPlayer; }
    public sealed class Alert { public Stock stock; public decimal price; public bool above; public int room; }
    public sealed class Call { public Stock stock; public decimal price; public int score; public double due; public int room; public long messageId; }
    public static readonly List<Job> Jobs = new();
    public static readonly List<Alert> Alerts = new();
    public static readonly List<Call> Calls = new();
    public static readonly Dictionary<Stock, List<(float at, int kind)>> Traces = new();
    public static readonly List<WhaleBot> Whales = new();
    /// <summary>0 기본, 1 정밀 분석. PlayerProgression이 저장하고 되살린다.</summary>
    public static int Tier;
    public static int Day = -1, AlertsToday, Right, Wrong, TotalRight, TotalWrong, BriefDay = -1;
    public static object Owner;
    public static int AlertLimit => Tier > 0 ? 6 : 3;
    public static void Reset(object owner)
    {
        Owner = owner; Jobs.Clear(); Alerts.Clear(); Calls.Clear(); Traces.Clear(); Whales.Clear();
        Day = -1; AlertsToday = Right = Wrong = TotalRight = TotalWrong = 0; BriefDay = -1;
    }
    /// <summary>작전 도구가 실제로 쓰였다(누가 썼는지는 적지 않는다).</summary>
    public static void Note(Stock stock, int kind)
    {
        if (stock == null) { return; }
        if (!Traces.TryGetValue(stock, out var list)) { Traces.Add(stock, list = new List<(float at, int kind)>(8)); }
        float now = SessionClock.Time;
        list.RemoveAll(each => now - each.at > 180f || each.at > now);
        list.Add((now, kind));
    }
    /// <summary>최근 TraceSeconds 동안 그 흔적이 몇 번 있었는가. ago는 가장 최근 것이 몇 초 전인지.</summary>
    public static int Count(Stock stock, int kind, out float ago)
    {
        ago = 0f; int count = 0;
        if (stock == null || !Traces.TryGetValue(stock, out var list)) { return 0; }
        float now = SessionClock.Time, latest = -1f;
        foreach (var each in list)
        {
            if (each.kind != kind || now - each.at > TraceSeconds || each.at > now) { continue; }
            count++; if (each.at > latest) { latest = each.at; }
        }
        if (count > 0) { ago = now - latest; }
        return count;
    }
}

public sealed partial class InvestorSociety
{
    private static readonly List<Order> aiOrders = new(64);
    private static readonly HashSet<decimal> aiLevels = new();
    private static readonly Regex AiPricePattern = new(@"([0-9][0-9,\.]*)\s*(돈|don|ドン|d)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public int AiReports { get; private set; }
    private static string AiName(Stock stock) => GameLocale.Text(stock.stockName ?? stock.symbol ?? "");
    private static string AiPercent(decimal percent) => percent.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%";
    private static string AiPrice(decimal price) => PriceTickPolicy.FormatMoney(price);
    private static string AiDon => TalkWord("돈", " DON", "ドン");
    private static string AiPoints(int score) => TalkWord(score + "점", score.ToString(), score + "点");
    private static string AiAdvice => TalkWord(" 참고용입니다.", " For reference only.", "参考情報です。");
    private static Trader AiPlayer => StockManager.Instance != null ? StockManager.Instance.playerAccount : null;

    /// <summary>플레이어가 새벽 관찰실에서 "!분석"이 아닌 AI 명령을 썼다. 잠깐 뒤에 답한다.</summary>
    private void QueueAiCommand(int room, PlayerTalk talk, long id, double now)
    {
        DawnAi.Jobs.Add(new DawnAi.Job { due = now + 1.2 + HypeRandom.Next(), room = room, command = talk.Command, argument = talk.Argument, stock = talk.Stock, other = talk.Other, replyTo = id, byPlayer = true });
    }

    private void TickDawnAi(double now)
    {
        if (!ReferenceEquals(DawnAi.Owner, this)) { DawnAi.Reset(this); }
        if (DawnAi.Day != day.Day) { DawnAi.Day = day.Day; DawnAi.AlertsToday = 0; DawnAi.Right = DawnAi.Wrong = 0; }
        for (int i = 0; i < DawnAi.Jobs.Count;)
        {
            if (DawnAi.Jobs[i].due > now) { i++; continue; }
            var job = DawnAi.Jobs[i]; DawnAi.Jobs.RemoveAt(i); RunAiCommand(job, now);
        }
        GradeAiCalls(now);
        CheckAiAlerts(now);
        TickAiBriefing(now);
    }

    private void RunAiCommand(DawnAi.Job job, double now)
    {
        switch (job.command)
        {
            case "whale": PostWhaleScan(job, now); break;
            case "alert": PostAlert(job, now); break;
            case "record": PostRecord(job); break;
            case "compare": PostCompare(job, now); break;
            case "news": PostNewsCheck(job, now); break;
            case "room": PostRoomRead(job, now); break;
            case "account": PostAccount(job, now); break;
            case "upgrade": PostUpgrade(job, now); break;
            default: PostAiHelp(job); break;
        }
    }
    private long PostAiText(DawnAi.Job job, string text, ChatAnalysisCard card = null)
    {
        long id = PostAnalyst(job.room, text, card, job.replyTo);
        AiReports++;
        TraceTalk("    > AI[" + job.command + "]: " + text + (card == null ? "" : "  {" + card.title + " · " + card.scoreText + " · " + card.badge + " · " + AiRows(card) + "}"));
        return id;
    }
    private static string AiRows(ChatAnalysisCard card)
    {
        if (card.captions == null) { return ""; }
        var parts = new List<string>();
        for (int i = 0; i < card.captions.Length; i++) { if (!string.IsNullOrEmpty(card.captions[i])) { parts.Add(card.captions[i] + "=" + (card.values != null && i < card.values.Length ? card.values[i] : "")); } }
        return string.Join(", ", parts);
    }
    private ChatAnalysisCard AiCard(string kind, string title, int score, string scoreText, string badge, int tone, int captionWidth)
        => new ChatAnalysisCard { kind = kind, title = title, score = Mathf.Clamp(score, 0, 100), scoreText = scoreText, badge = badge, tone = tone, captionWidth = captionWidth,
            captions = new[] { "", "", "", "" }, values = new[] { "", "", "", "" }, tones = new int[4], capturedAt = SocialTime.GameMinute };
    private static void AiRow(ChatAnalysisCard card, int index, string caption, string value, int tone = 0)
    {
        if (index < 0 || index >= 4) { return; }
        card.captions[index] = caption ?? ""; card.values[index] = value ?? ""; card.tones[index] = tone;
    }
    private bool AiNeedsStock(DawnAi.Job job, string exampleKo, string exampleEn, string exampleJa)
    {
        if (job.stock != null) { return false; }
        PostAiText(job, string.IsNullOrEmpty(job.argument)
            ? TalkWord("종목 이름을 함께 적어 주세요. 예: " + exampleKo, "Add a stock name. Example: " + exampleEn, "銘柄名を付けてください。例: " + exampleJa)
            : TalkWord("'" + job.argument + "'에 맞는 종목을 찾지 못했습니다.", "No stock matches '" + job.argument + "'.", "「" + job.argument + "」に合う銘柄が見つかりません。"));
        return true;
    }

    // ── !세력: 수상한 움직임 ──
    /// <summary>세력과 플레이어 가운데 한 손이 가장 여러 호가에 나눠 걸어 둔 주문의 호가 수(현재가 위아래 6% 안). 누구인지는 돌려주지 않는다.</summary>
    private int SplitLevels(Stock stock, bool buy, out decimal shares)
    {
        shares = 0m; int widest = 0; decimal most = 0m;
        if (stock == null || stock.orderBook == null || stock.currentPrice <= 0m) { return 0; }
        decimal low = stock.currentPrice * .94m, high = stock.currentPrice * 1.06m;
        if (DawnAi.Whales.Count == 0)
        {
            foreach (var bot in day.Bots) { if (bot is WhaleBot whale) { DawnAi.Whales.Add(whale); } }
            if (DawnAi.Whales.Count == 0) { DawnAi.Whales.AddRange(Object.FindObjectsByType<WhaleBot>()); }
        }
        var owners = new List<IOrderOwner>(DawnAi.Whales.Count + 2);
        foreach (var whale in DawnAi.Whales) { if (whale != null) { owners.Add(whale); } }
        if (PlayerAccounts.Instance != null) { if (PlayerAccounts.Instance.primary != null) { owners.Add(PlayerAccounts.Instance.primary); } if (PlayerAccounts.Instance.automated != null) { owners.Add(PlayerAccounts.Instance.automated); } }
        else if (AiPlayer != null) { owners.Add(AiPlayer); }
        foreach (var owner in owners)
        {
            stock.orderBook.CopyOwnerOrders(owner, aiOrders); aiLevels.Clear(); decimal total = 0m;
            foreach (var order in aiOrders)
            {
                if (order.isBuy != buy || order.volume <= 0m || order.price < low || order.price > high) { continue; }
                aiLevels.Add(order.price); total += order.volume;
            }
            if (aiLevels.Count > widest) { widest = aiLevels.Count; most = total; }
        }
        shares = most;
        return widest;
    }
    /// <summary>수상한 정도(0~100)와 그 까닭. 호가에 잘게 나뉜 주문, 대량으로 쓸어 올리거나 내린 체결, 세웠다 거둔 매수벽, 주고받는 거래를 본다.</summary>
    public ChatAnalysisCard WhaleScan(Stock stock, out List<string> findings)
    {
        findings = new List<string>();
        if (stock == null || stock.currentPrice <= 0m) { return null; }
        int askLevels = SplitLevels(stock, false, out _), bidLevels = SplitLevels(stock, true, out _);
        int slams = DawnAi.Count(stock, DawnAi.Slam, out float slamAgo), ignites = DawnAi.Count(stock, DawnAi.Ignite, out float igniteAgo), nips = DawnAi.Count(stock, DawnAi.Nip, out _);
        int pulled = DawnAi.Count(stock, DawnAi.WallPulled, out _), raised = DawnAi.Count(stock, DawnAi.WallUp, out _), washes = DawnAi.Count(stock, DawnAi.Wash, out _);
        float heat = StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock);
        int score = 5;
        if (askLevels >= 5) { score += 30 + Math.Min(15, (askLevels - 5) * 3); findings.Add(TalkWord("매도 주문이 " + askLevels + "개 호가에 잘게 나뉘어 걸려 있습니다.", "Sell orders are sliced thin across " + askLevels + " price levels.", "売り注文が" + askLevels + "本の気配に細かく分けて並んでいます。")); }
        if (slams > 0) { score += 30; findings.Add(TalkWord(Mathf.RoundToInt(slamAgo) + "초 전 매수 호가를 한 번에 쓸어내린 대량 매도가 있었습니다.", "A large sell swept the bids " + Mathf.RoundToInt(slamAgo) + "s ago.", Mathf.RoundToInt(slamAgo) + "秒前に買い板を一気に叩く大口売りがありました。")); }
        if (ignites > 0) { score += 28; findings.Add(TalkWord(Mathf.RoundToInt(igniteAgo) + "초 전 매도 호가를 한 번에 쓸어 올린 대량 매수가 있었습니다.", "A large buy swept the offers " + Mathf.RoundToInt(igniteAgo) + "s ago.", Mathf.RoundToInt(igniteAgo) + "秒前に売り板を一気に買い上げる大口買いがありました。")); }
        if (pulled > 0) { score += 25; findings.Add(TalkWord("큰 매수벽이 체결 없이 사라졌습니다.", "A large bid wall vanished without trading.", "大きな買い板が約定しないまま消えました。")); }
        else if (raised > 0) { score += 12; findings.Add(TalkWord("아래에 큰 매수벽이 새로 세워졌습니다.", "A large bid wall was just placed underneath.", "下に大きな買い板が新しく置かれました。")); }
        if (washes > 0) { score += 22; findings.Add(TalkWord("같은 값에서 주고받는 거래가 되풀이됩니다.", "Trades keep printing back and forth at the same price.", "同じ値段で売り買いが繰り返されています。")); }
        if (nips > 0) { score += 14; findings.Add(TalkWord("오르는 중에 매수 호가를 조금씩 받아 치는 매도가 섞여 있습니다.", "Someone is quietly selling into the bids on the way up.", "上げの途中で買い板に少しずつぶつける売りが混じっています。")); }
        if (bidLevels >= 5) { score += 12; findings.Add(TalkWord("매수 주문이 " + bidLevels + "개 호가에 나뉘어 받치고 있습니다.", "Buy orders are layered across " + bidLevels + " price levels.", "買い注文が" + bidLevels + "本の気配に分けて支えています。")); }
        if (heat >= 3f && findings.Count > 0) { score += 8; }
        score = Mathf.Clamp(score, 3, 97);
        string badge = score >= 60 ? TalkWord("작전 의심", "Suspicious", "仕手の疑い") : score >= 30 ? TalkWord("주의", "Caution", "注意") : TalkWord("조용함", "Quiet", "静か");
        var card = AiCard("whale", AiName(stock), score, score.ToString(), badge, score >= 60 ? 4 : score >= 30 ? 0 : 3, 110);
        card.symbol = stock.symbol; card.stockName = stock.stockName; card.price = stock.currentPrice;
        string levels = TalkWord("호가", " levels", "本");
        AiRow(card, 0, TalkWord("매도 분할", "Split asks", "売り分割"), askLevels >= 2 ? askLevels + levels : TalkWord("없음", "None", "なし"), askLevels >= 5 ? 4 : 0);
        AiRow(card, 1, TalkWord("매수 분할", "Split bids", "買い分割"), bidLevels >= 2 ? bidLevels + levels : TalkWord("없음", "None", "なし"), bidLevels >= 5 ? 4 : 0);
        int sweeps = slams + ignites + nips;
        AiRow(card, 2, TalkWord("대량 체결", "Sweeps", "大口約定"), sweeps > 0 ? TalkWord(sweeps + "회", sweeps + "x", sweeps + "回") : TalkWord("없음", "None", "なし"), sweeps > 0 ? 4 : 0);
        AiRow(card, 3, TalkWord("매수벽", "Bid wall", "買い板"), pulled > 0 ? TalkWord("사라짐", "Pulled", "消えた") : raised > 0 ? TalkWord("세워짐", "Placed", "置かれた") : TalkWord("변화 없음", "No change", "変化なし"), pulled > 0 ? 4 : 0);
        return card;
    }
    private void PostWhaleScan(DawnAi.Job job, double now)
    {
        if (AiNeedsStock(job, "!세력 노보", "!whale Novo", "!仕手 ノボ")) { return; }
        var card = WhaleScan(job.stock, out var findings);
        if (card == null) { return; }
        string name = AiName(job.stock); int shown = DawnAi.Tier > 0 ? 4 : 2;
        string body = findings.Count == 0 ? TalkWord("눈에 띄는 움직임이 없습니다.", "Nothing unusual right now.", "目立った動きはありません。") : string.Join(" ", findings.GetRange(0, Math.Min(shown, findings.Count)));
        string tail = findings.Count == 0 ? "" : TalkWord(" 누가 하는지는 알 수 없습니다.", " I can't tell who is behind it.", "誰の仕業かは分かりません。");
        long id = PostAiText(job, TalkWord(name + " 최근 2분의 호가와 체결입니다. " + body + tail, name + ", last two minutes of book and tape. " + body + tail, name + "の直近2分の板と約定です。" + body + tail), card);
        Plan(job.room, card.score >= 60 ? "aiReact.whale" : card.score >= 30 ? "aiReact.whaleMaybe" : "aiReact.whaleNone", job.stock, id, now + 1.6, 0, card.score >= 60 ? 2 : 1, card.score >= 60 ? .75f : .45f);
        // 수상하다는 말이 나오면 방의 눈이 그 종목으로 조금 쏠린다.
        if (card.score >= 60) { Sway(job.room, job.stock, .5f); }
    }

    // ── !알림: 가격 감시 ──
    private void PostAlert(DawnAi.Job job, double now)
    {
        string argument = (job.argument ?? "").Trim(), lower = argument.ToLowerInvariant();
        if (argument.Length == 0)
        {
            if (DawnAi.Alerts.Count == 0)
            {
                PostAiText(job, TalkWord("걸어 둔 알림이 없습니다. 예: !알림 노보 1000 (오늘 " + (DawnAi.AlertLimit - DawnAi.AlertsToday) + "건 남음)", "No alerts set. Example: !alert Novo 1000 (" + (DawnAi.AlertLimit - DawnAi.AlertsToday) + " left today)",
                    "通知はありません。例: !通知 ノボ 1000（本日残り" + (DawnAi.AlertLimit - DawnAi.AlertsToday) + "件）"));
                return;
            }
            var parts = new List<string>();
            foreach (var each in DawnAi.Alerts) { parts.Add(AiName(each.stock) + " " + AiPrice(each.price) + (each.above ? "↑" : "↓")); }
            PostAiText(job, TalkWord("걸어 둔 알림: ", "Alerts set: ", "設定中の通知: ") + string.Join(", ", parts) + TalkWord(". 지우려면 !알림 취소", ". Clear with !alert cancel", "。消すには !通知 取消"));
            return;
        }
        if (lower == "취소" || lower == "삭제" || lower == "cancel" || lower == "clear" || lower == "off" || lower == "取消" || lower == "解除")
        {
            int cleared = DawnAi.Alerts.Count; DawnAi.Alerts.Clear();
            PostAiText(job, TalkWord("알림 " + cleared + "건을 지웠습니다.", "Cleared " + cleared + " alert(s).", "通知を" + cleared + "件消しました。"));
            return;
        }
        var match = AiPricePattern.Match(argument); decimal target = 0m;
        if (match.Success) { decimal.TryParse(match.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out target); }
        var stock = PlayerTalk.FindStock(match.Success ? argument.Substring(0, match.Index) : argument, true);
        if (stock == null || target <= 0m)
        {
            PostAiText(job, TalkWord("종목과 값을 함께 적어 주세요. 예: !알림 노보 1000", "Give me a stock and a price. Example: !alert Novo 1000", "銘柄と値段を一緒に書いてください。例: !通知 ノボ 1000"));
            return;
        }
        string name = AiName(stock);
        if (stock.currentPrice <= 0m || target == stock.currentPrice)
        {
            PostAiText(job, TalkWord(name + " 지금 값이 이미 " + AiPrice(stock.currentPrice) + "입니다.", name + " is already there (" + AiPrice(stock.currentPrice) + ").", name + "はいまその値段です（" + AiPrice(stock.currentPrice) + "）。"));
            return;
        }
        if (DawnAi.AlertsToday >= DawnAi.AlertLimit)
        {
            PostAiText(job, TalkWord("오늘 알림 " + DawnAi.AlertLimit + "건을 다 썼습니다." + (DawnAi.Tier > 0 ? "" : " 정밀 분석(!업그레이드)이면 하루 6건입니다."),
                "You've used all " + DawnAi.AlertLimit + " alerts for today." + (DawnAi.Tier > 0 ? "" : " Precision mode (!upgrade) gives 6 a day."),
                "本日の通知" + DawnAi.AlertLimit + "件を使い切りました。" + (DawnAi.Tier > 0 ? "" : "精密分析（!アップグレード）なら1日6件です。")));
            return;
        }
        DawnAi.AlertsToday++;
        DawnAi.Alerts.Add(new DawnAi.Alert { stock = stock, price = target, above = target > stock.currentPrice, room = job.room });
        int left = DawnAi.AlertLimit - DawnAi.AlertsToday;
        PostAiText(job, TalkWord(name + " " + AiPrice(target) + " 알림을 걸었습니다(지금 " + AiPrice(stock.currentPrice) + "). 닿으면 이 방에서 부르겠습니다. 오늘 " + left + "건 남았습니다.",
            "Alert set: " + name + " at " + AiPrice(target) + " (now " + AiPrice(stock.currentPrice) + "). I'll call you here when it gets there. " + left + " left today.",
            name + " " + AiPrice(target) + "に通知を設定しました（現在" + AiPrice(stock.currentPrice) + "）。届いたらこの部屋で呼びます。本日残り" + left + "件です。"));
    }
    private void CheckAiAlerts(double now)
    {
        for (int i = DawnAi.Alerts.Count - 1; i >= 0; i--)
        {
            var alert = DawnAi.Alerts[i];
            if (alert.stock == null || !alert.stock.IsListed || alert.stock.IsDelisted) { DawnAi.Alerts.RemoveAt(i); continue; }
            decimal price = alert.stock.currentPrice;
            if (price <= 0m || (alert.above ? price < alert.price : price > alert.price)) { continue; }
            DawnAi.Alerts.RemoveAt(i);
            string name = AiName(alert.stock);
            var card = Analyze(alert.stock, out _);
            string text = YouMention + " " + TalkWord("알림입니다. " + name + " " + AiPrice(alert.price) + " 도달. 지금 " + AiPrice(price) + "입니다.",
                "Alert: " + name + " hit " + AiPrice(alert.price) + ". Now " + AiPrice(price) + ".", "通知です。" + name + "が" + AiPrice(alert.price) + "に到達。現在" + AiPrice(price) + "です。");
            long id = PostAnalyst(alert.room, text, card, 0);
            AiReports++;
            TraceTalk("    > AI[alert]: " + text);
            MarketEventFeed.Instance?.Notify(TalkWord("AI 알림 · " + name + " " + AiPrice(alert.price) + " 도달", "AI alert · " + name + " hit " + AiPrice(alert.price), "AI通知 · " + name + " " + AiPrice(alert.price) + "到達"));
            Plan(alert.room, alert.above ? "aiReact.alertUp" : "aiReact.alertDown", alert.stock, id, now + 1.5, 0, 1, .5f);
        }
    }

    // ── 성적: 분석이 맞았는가 ──
    /// <summary>방금 올린 분석(매수·매도 의견만)을 채점 목록에 올린다. 성적이 좋으면 매수 의견에 방의 눈이 쏠린다.</summary>
    private void NoteAiCall(int room, Stock stock, ChatAnalysisCard card, long id, double now)
    {
        if (stock == null || card == null || card.price <= 0m || card.score >= 42 && card.score < 58) { return; }
        DawnAi.Calls.Add(new DawnAi.Call { stock = stock, price = card.price, score = card.score, due = now + DawnAi.GradeSeconds, room = room, messageId = id });
        while (DawnAi.Calls.Count > 24) { DawnAi.Calls.RemoveAt(0); }
        int graded = DawnAi.TotalRight + DawnAi.TotalWrong;
        if (card.score >= 58 && graded >= 3 && DawnAi.TotalRight * 10 >= graded * 6) { Sway(room, stock, .3f + .5f * DawnAi.TotalRight / graded); }
    }
    private void GradeAiCalls(double now)
    {
        for (int i = DawnAi.Calls.Count - 1; i >= 0; i--)
        {
            var call = DawnAi.Calls[i];
            if (now < call.due) { continue; }
            DawnAi.Calls.RemoveAt(i);
            if (call.stock == null || !call.stock.AcceptsOrders || call.price <= 0m || call.stock.currentPrice <= 0m) { continue; }
            decimal move = call.stock.currentPrice / call.price - 1m; bool buy = call.score >= 58;
            bool right = buy ? move >= DawnAi.GradeMove : move <= -DawnAi.GradeMove, wrong = buy ? move <= -DawnAi.GradeMove : move >= DawnAi.GradeMove;
            if (!right && !wrong) { continue; }
            if (right) { DawnAi.Right++; DawnAi.TotalRight++; } else { DawnAi.Wrong++; DawnAi.TotalWrong++; }
            TraceTalk("    (AI 분석 " + (right ? "적중" : "빗나감") + ": " + call.stock.symbol + " " + (buy ? "매수" : "매도") + " 의견, " + (move * 100m).ToString("+0.0;-0.0") + "%)");
            // 맞으면 감탄하고 틀리면 놀린다. 매번은 아니다.
            Plan(call.room, right ? "aiReact.right" : "aiReact.wrong", call.stock, call.messageId, now + .6, 0, 1, .55f, (float)(move * 100m));
        }
    }
    private void PostRecord(DawnAi.Job job)
    {
        int today = DawnAi.Right + DawnAi.Wrong, total = DawnAi.TotalRight + DawnAi.TotalWrong, waiting = DawnAi.Calls.Count;
        if (total == 0)
        {
            PostAiText(job, TalkWord("아직 채점된 분석이 없습니다" + (waiting > 0 ? "(채점 대기 " + waiting + "건)" : "") + ". 매수·매도 의견만, 2분 뒤 그 방향으로 1% 넘게 갔는지로 채점합니다.",
                "Nothing graded yet" + (waiting > 0 ? " (" + waiting + " pending)" : "") + ". Only buy and sell calls are graded: did it move over 1% that way two minutes later.",
                "まだ採点された分析はありません" + (waiting > 0 ? "（採点待ち" + waiting + "件）" : "") + "。買い・売りの判断だけを、2分後にその方向へ1%超動いたかで採点します。"));
            return;
        }
        int right = today > 0 ? DawnAi.Right : DawnAi.TotalRight, count = today > 0 ? today : total, rate = Mathf.RoundToInt(right * 100f / count);
        var card = AiCard("record", TalkWord("AI 분석 성적", "AI track record", "AI分析の成績"), rate, rate + "%", TalkWord("적중률", "Hit rate", "的中率"), rate >= 60 ? 1 : rate < 40 ? -1 : 0, 110);
        string cases = TalkWord("건", "", "件");
        AiRow(card, 0, TalkWord("오늘 적중", "Hits today", "本日の的中"), DawnAi.Right + cases, 1);
        AiRow(card, 1, TalkWord("오늘 빗나감", "Misses today", "本日の外れ"), DawnAi.Wrong + cases, -1);
        AiRow(card, 2, TalkWord("채점 대기", "Pending", "採点待ち"), waiting + cases);
        AiRow(card, 3, TalkWord("누적", "All time", "累計"), DawnAi.TotalRight + " / " + total);
        string scope = today > 0 ? TalkWord("오늘", "today", "本日") : TalkWord("지금까지", "so far", "これまで");
        PostAiText(job, TalkWord(scope + " 채점한 " + count + "건 중 " + right + "건 적중입니다(적중률 " + rate + "%). 매수·매도 의견만, 2분 뒤 1% 넘게 움직였는지로 채점합니다.",
            right + " of " + count + " graded calls hit " + scope + " (" + rate + "%). Only buy and sell calls count: did it move over 1% two minutes later.",
            scope + "採点した" + count + "件のうち" + right + "件的中です（的中率" + rate + "%）。買い・売りの判断だけを、2分後に1%超動いたかで採点します。"), card);
    }

    // ── !비교 ──
    private void PostCompare(DawnAi.Job job, double now)
    {
        Stock first = job.stock, second = job.other;
        if (first == null || second == null || first == second)
        {
            PostAiText(job, TalkWord("종목 두 개를 적어 주세요. 예: !비교 노보 산전", "Give me two stocks. Example: !compare Novo LC", "銘柄を二つ書いてください。例: !比較 ノボ LC"));
            return;
        }
        var one = Analyze(first, out _); var two = Analyze(second, out _);
        if (one == null || two == null) { return; }
        string nameOne = AiName(first), nameTwo = AiName(second);
        bool close = Math.Abs(one.score - two.score) < 5; var better = one.score >= two.score ? one : two; string betterName = one.score >= two.score ? nameOne : nameTwo;
        var card = AiCard("compare", nameOne + " vs " + nameTwo, better.score, better.score.ToString(), close ? TalkWord("비슷함", "Even", "互角") : TabletGroupChatUI.AnalysisVerdict(better.score), 2, 170);
        decimal moveOne = DayMove(first), moveTwo = DayMove(second);
        AiRow(card, 0, nameOne, AiPoints(one.score), one.score >= 58 ? 1 : one.score < 42 ? -1 : 0);
        AiRow(card, 1, nameTwo, AiPoints(two.score), two.score >= 58 ? 1 : two.score < 42 ? -1 : 0);
        AiRow(card, 2, nameOne, AiPercent(moveOne), Math.Sign(moveOne));
        AiRow(card, 3, nameTwo, AiPercent(moveTwo), Math.Sign(moveTwo));
        string verdict = close ? TalkWord("둘이 비슷합니다.", "They read about the same.", "ほぼ互角です。") : TalkWord("지금은 " + betterName + " 쪽이 낫게 보입니다.", betterName + " reads better right now.", "いまは" + betterName + "のほうが良く見えます。");
        long id = PostAiText(job, TalkWord(nameOne + " " + one.score + "점, " + nameTwo + " " + two.score + "점입니다. " + verdict + AiAdvice,
            nameOne + " " + one.score + ", " + nameTwo + " " + two.score + ". " + verdict + AiAdvice, nameOne + " " + one.score + "点、" + nameTwo + " " + two.score + "点です。" + verdict + AiAdvice), card);
        var pick = one.score >= two.score ? first : second;
        Plan(job.room, close ? "aiReact.hold" : better.score >= 58 ? "aiReact.buy" : "aiReact.hold", pick, id, now + 1.6, 0, 1, .5f);
    }

    // ── !뉴스: 기사 판정 ──
    private void PostNewsCheck(DawnAi.Job job, double now)
    {
        if (AiNeedsStock(job, "!뉴스 노보", "!news Novo", "!ニュース ノボ")) { return; }
        string name = AiName(job.stock); var article = LatestNews(job.stock);
        if (article == null)
        {
            PostAiText(job, TalkWord(name + "의 최근 기사를 찾지 못했습니다.", "No recent article on " + name + ".", name + "の最近の記事は見つかりません。"));
            return;
        }
        int score = Mathf.Clamp(Mathf.RoundToInt((float)article.credibility * 100f), 3, 97);
        string badge = score >= 75 ? TalkWord("신뢰", "Reliable", "信頼") : score >= 50 ? TalkWord("보통", "Fair", "普通") : TalkWord("의심", "Doubtful", "疑わしい");
        string source = GameLocale.Text(article.source ?? ""), check = GameLocale.Text(article.verification ?? ""), title = GameLocale.Text(article.title ?? "");
        int cut = source.IndexOf(" · ", StringComparison.Ordinal); string shortSource = cut > 0 ? source.Substring(0, cut) : source;
        var card = AiCard("news", name, score, score.ToString(), badge, score >= 75 ? 3 : score >= 50 ? 0 : 4, 76);
        card.symbol = job.stock.symbol; card.stockName = job.stock.stockName;
        string tone = article.sentiment > .15 ? TalkWord("호재", "Positive", "好材料") : article.sentiment < -.15 ? TalkWord("악재", "Negative", "悪材料") : TalkWord("중립", "Neutral", "中立");
        AiRow(card, 0, TalkWord("출처", "Source", "出所"), shortSource, score < 50 ? 4 : 0);
        AiRow(card, 1, TalkWord("확인", "Status", "確認"), check, score < 50 ? 4 : 0);
        AiRow(card, 2, TalkWord("논조", "Tone", "論調"), tone, article.sentiment > .15 ? 1 : article.sentiment < -.15 ? -1 : 0);
        var clock = Regex.Match(article.time ?? "", @"\d{1,2}:\d{2}");
        AiRow(card, 3, TalkWord("시각", "Time", "時刻"), clock.Success ? clock.Value : "");
        string warn = score < 50 ? TalkWord(" 출처가 확인되지 않은 보도입니다. 그대로 믿지 마세요.", " The source is unverified. Don't take it at face value.", "出所が確認されていない報道です。鵜呑みにしないでください。")
            : score >= 90 ? TalkWord(" 확인된 내용입니다.", " This one is confirmed.", "確認済みの内容です。") : TalkWord(" 통상적인 보도입니다.", " Routine coverage.", "通常の報道です。");
        long id = PostAiText(job, TalkWord("「" + title + "」 출처는 " + source + "입니다. 신뢰도 " + score + "점." + warn, "\"" + title + "\" Source: " + source + ". Reliability " + score + "." + warn,
            "「" + title + "」出所は" + source + "です。信頼度" + score + "点。" + warn), card);
        Plan(job.room, score < 50 ? "aiReact.newsFake" : "aiReact.newsOk", job.stock, id, now + 1.6, 0, score < 50 ? 2 : 1, score < 50 ? .7f : .4f);
    }

    // ── !방: 방의 쏠림 ──
    private void PostRoomRead(DawnAi.Job job, double now)
    {
        int members = 0; float greed = 0f, fear = 0f; var holders = new Dictionary<Stock, int>();
        foreach (var bot in socialBots)
        {
            if (bot == null || bot.Social == null || !bot.isActiveAndEnabled || bot.Social.Psychology.Room != job.room) { continue; }
            members++; greed += bot.Social.Psychology.Greed; fear += bot.Social.Psychology.Fear;
            // 세력·종목 대표의 지분은 세지 않는다(그 정보는 따로 알아내야 한다).
            if (bot.KnowsRealValue || bot is WhaleBot || StockManager.Instance == null) { continue; }
            foreach (var stock in StockManager.Instance.stocks)
            {
                if (stock == null || stock.IsDelisted || bot.GetOwnedVolume(stock) < 1m) { continue; }
                holders.TryGetValue(stock, out int count); holders[stock] = count + 1;
            }
        }
        if (members == 0)
        {
            PostAiText(job, TalkWord("지금 방에 읽을 사람이 없습니다.", "Nobody in the room to read right now.", "いま部屋に読める人がいません。"));
            return;
        }
        greed /= members; fear /= members;
        int lean = Mathf.Clamp(Mathf.RoundToInt(greed + fear <= 0f ? 50f : greed / (greed + fear) * 100f), 3, 97);
        var ranked = new List<KeyValuePair<Stock, int>>(holders); ranked.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key.symbol, b.Key.symbol));
        string badge = lean >= 60 ? TalkWord("탐욕 우세", "Greedy", "強欲優勢") : lean <= 40 ? TalkWord("공포 우세", "Fearful", "恐怖優勢") : TalkWord("팽팽함", "Balanced", "拮抗");
        var card = AiCard("room", TalkWord("방 분위기 · " + members + "명", "Room mood · " + members + " people", "部屋の空気 · " + members + "人"), lean, lean.ToString(), badge, lean >= 60 ? 1 : lean <= 40 ? -1 : 0, 170);
        for (int i = 0; i < 3 && i < ranked.Count; i++) { AiRow(card, i, AiName(ranked[i].Key), Mathf.RoundToInt(ranked[i].Value * 100f / members) + "%"); }
        AiRow(card, 3, TalkWord("탐욕 / 공포", "Greed / Fear", "強欲 / 恐怖"), Mathf.RoundToInt(greed) + " / " + Mathf.RoundToInt(fear));
        string top = ranked.Count == 0 ? TalkWord("들고 있는 종목이 거의 없습니다.", "Hardly anyone is holding anything.", "ほとんど誰も持っていません。")
            : TalkWord(Mathf.RoundToInt(ranked[0].Value * 100f / members) + "%가 " + AiName(ranked[0].Key) + " 보유 중입니다.", Mathf.RoundToInt(ranked[0].Value * 100f / members) + "% are holding " + AiName(ranked[0].Key) + ".",
                Mathf.RoundToInt(ranked[0].Value * 100f / members) + "%が" + AiName(ranked[0].Key) + "を持っています。");
        string mood = lean >= 60 ? TalkWord(" 다들 들떠 있습니다. 쏠린 쪽이 먼저 무너지곤 합니다.", " The room is excited. Crowded sides tend to break first.", "みんな浮かれています。偏った側から崩れがちです。")
            : lean <= 40 ? TalkWord(" 다들 겁먹었습니다.", " The room is scared.", "みんな怯えています。") : TalkWord(" 한쪽으로 쏠리지는 않았습니다.", " No clear lean either way.", "どちらにも偏っていません。");
        long id = PostAiText(job, TalkWord("지금 방 " + members + "명 중 " + top + mood, "Of " + members + " people here, " + top + mood, "いま部屋の" + members + "人のうち" + top + mood), card);
        Plan(job.room, "aiReact.room", ranked.Count > 0 ? ranked[0].Key : null, id, now + 1.6, 0, 1, .6f);
    }

    // ── !내계좌: 보유 진단 ──
    private void PostAccount(DawnAi.Job job, double now)
    {
        var account = AiPlayer; var held = new List<(Stock stock, decimal value, decimal percent)>(); decimal cost = 0m, worth = 0m;
        if (account != null && StockManager.Instance != null)
        {
            foreach (var stock in StockManager.Instance.stocks)
            {
                if (stock == null || stock.currentPrice <= 0m) { continue; }
                decimal quantity = account.GetOwnedVolume(stock), average = account.GetOwnedAverage(stock);
                if (quantity < 1m || average <= 0m) { continue; }
                held.Add((stock, quantity * stock.currentPrice, (stock.currentPrice / average - 1m) * 100m));
                cost += quantity * average; worth += quantity * stock.currentPrice;
            }
        }
        if (held.Count == 0 || cost <= 0m)
        {
            PostAiText(job, TalkWord("보유 종목이 없습니다. 진단할 것이 없습니다.", "You're not holding anything. Nothing to check.", "保有銘柄がありません。診断するものがありません。"));
            return;
        }
        held.Sort((a, b) => b.value.CompareTo(a.value));
        decimal total = (worth / cost - 1m) * 100m, weight = held[0].value / worth * 100m;
        var worst = held[0]; foreach (var each in held) { if (each.percent < worst.percent) { worst = each; } }
        int score = Mathf.Clamp(Mathf.RoundToInt(55f + (float)total * 2.5f - (weight >= 80m && held.Count > 1 ? 8f : 0f) - (worst.percent <= -10m ? 10f : 0f)), 3, 97);
        string badge = score >= 65 ? TalkWord("양호", "Healthy", "良好") : score >= 40 ? TalkWord("보통", "Fair", "普通") : TalkWord("위험", "At risk", "危険");
        var card = AiCard("account", TalkWord("내 계좌 · " + held.Count + "종목", "My account · " + held.Count + " stocks", "自分の口座 · " + held.Count + "銘柄"), score, AiPercent(total), badge, total > 0m ? 1 : total < 0m ? -1 : 0, 170);
        for (int i = 0; i < 4 && i < held.Count; i++) { AiRow(card, i, AiName(held[i].stock), AiPercent(held[i].percent), Math.Sign(held[i].percent)); }
        string advice = worst.percent <= -10m ? TalkWord(" " + AiName(worst.stock) + " " + AiPercent(worst.percent) + "입니다. 손절 기준을 정해 두세요.", " " + AiName(worst.stock) + " is at " + AiPercent(worst.percent) + ". Decide where you cut it.",
                " " + AiName(worst.stock) + "が" + AiPercent(worst.percent) + "です。損切りの基準を決めておきましょう。")
            : weight >= 80m && held.Count > 1 ? TalkWord(" 한 종목에 너무 쏠려 있습니다.", " Too much of it sits in one stock.", "一銘柄に偏りすぎています。")
            : total >= 10m ? TalkWord(" 수익이 났을 때 일부는 챙겨 두세요.", " Consider locking in part of the gain.", "利益が出ているうちに一部は確定を。") : "";
        long id = PostAiText(job, TalkWord("보유 " + held.Count + "종목, 평가손익 " + AiPrice(worth - cost) + AiDon + "(" + AiPercent(total) + ")입니다. 가장 큰 비중은 " + AiName(held[0].stock) + " " + weight.ToString("0") + "%입니다." + advice,
            held.Count + " holdings, open P/L " + AiPrice(worth - cost) + AiDon + " (" + AiPercent(total) + "). Biggest position: " + AiName(held[0].stock) + " at " + weight.ToString("0") + "%." + advice,
            "保有" + held.Count + "銘柄、評価損益" + AiPrice(worth - cost) + AiDon + "（" + AiPercent(total) + "）です。最大の比重は" + AiName(held[0].stock) + " " + weight.ToString("0") + "%です。" + advice), card);
        // 방에 다 보인다. 잘 벌고 있으면 부러워하고, 물려 있으면 한마디씩 한다.
        Plan(job.room, total >= 3m ? "aiReact.accountUp" : total <= -3m ? "aiReact.accountDown" : "aiReact.accountFlat", total <= -3m ? worst.stock : held[0].stock, id, now + 1.6, 0, 2, .65f, (float)total);
    }

    // ── !업그레이드: 정밀 분석 ──
    private void PostUpgrade(DawnAi.Job job, double now)
    {
        string argument = (job.argument ?? "").Trim().ToLowerInvariant(); string cost = AiPrice(DawnAi.UpgradeCost) + AiDon;
        if (DawnAi.Tier > 0)
        {
            PostAiText(job, TalkWord("이미 정밀 분석을 쓰고 있습니다.", "Precision mode is already on.", "すでに精密分析をご利用中です。"));
            return;
        }
        if (argument != "확인" && argument != "confirm" && argument != "yes" && argument != "確認")
        {
            PostAiText(job, TalkWord("정밀 분석: 분석 근거를 세 가지로 늘리고 5분 흐름까지 봅니다. 세력 탐지는 네 가지까지, 알림은 하루 6건입니다. 값은 " + cost + "(한 번)입니다. 사려면 '!업그레이드 확인'을 입력하세요.",
                "Precision mode: three reasons per analysis plus the 5-minute trend, up to four whale findings, six alerts a day. One-time cost " + cost.Trim() + ". Type '!upgrade confirm' to buy.",
                "精密分析: 分析の根拠を三つに増やし、5分の流れまで見ます。仕手検知は四つまで、通知は1日6件。料金は" + cost + "（一回）です。購入は「!アップグレード 確認」と入力してください。"));
            return;
        }
        var payer = AiPlayer;
        if (!job.byPlayer || payer == null || PlayerAccounts.Available(payer) < DawnAi.UpgradeCost)
        {
            PostAiText(job, TalkWord("주문 가능 현금이 모자랍니다. " + cost + "이 필요합니다.", "Not enough available cash. It costs " + cost.Trim() + ".", "注文可能な現金が足りません。" + cost + "が必要です。"));
            return;
        }
        payer.cash -= DawnAi.UpgradeCost; DawnAi.Tier = 1;
        long id = PostAiText(job, TalkWord("정밀 분석을 켰습니다. " + cost + "을 받았습니다. 지금부터 더 자세히 봅니다.", "Precision mode is on. Charged " + cost.Trim() + ". I'll look closer from now on.", "精密分析をオンにしました。" + cost + "をいただきました。これからはより詳しく見ます。"));
        Plan(job.room, "aiReact.upgrade", null, id, now + 1.6, 0, 2, .7f);
    }
    /// <summary>정밀 분석에서 덧붙이는 근거: 5분 흐름, 당일 고저 사이의 자리, 호가 잔량의 기울기.</summary>
    private string AnalyzeMore(Stock stock, ChatAnalysisCard card)
    {
        var parts = new List<string>();
        if (chartTracks.TryGetValue(stock, out var track) && PriceAgo(track, SocialTime.Seconds, 300, out decimal before) && before > 0m)
        {
            string move = AiPercent((stock.currentPrice / before - 1m) * 100m);
            parts.Add(TalkWord("최근 5분은 " + move + "입니다.", "Last five minutes: " + move + ".", "直近5分は" + move + "です。"));
        }
        if (card.high > card.low)
        {
            int spot = Mathf.RoundToInt((float)((card.price - card.low) / (card.high - card.low)) * 100f);
            parts.Add(TalkWord("당일 저가와 고가 사이 " + spot + "% 자리입니다.", "Sitting " + spot + "% of the way from the day's low to its high.", "本日の安値から高値の" + spot + "%の位置です。"));
        }
        int tilt = ManipulationTools.BookTilt(stock);
        parts.Add(tilt > 0 ? TalkWord("호가 잔량은 매수 쪽이 두껍습니다.", "The book is heavier on the bid.", "板は買いのほうが厚いです。")
            : tilt < 0 ? TalkWord("호가 잔량은 매도 쪽이 두껍습니다.", "The book is heavier on the offer.", "板は売りのほうが厚いです。") : TalkWord("호가 잔량은 양쪽이 비슷합니다.", "The book is balanced.", "板は両側ほぼ同じです。"));
        return string.Join(" ", parts);
    }

    // ── 도움말 ──
    private void PostAiHelp(DawnAi.Job job)
    {
        PostAiText(job, TalkWord("쓸 수 있는 명령: !분석 종목 · !세력 종목 · !비교 종목 종목 · !뉴스 종목 · !알림 종목 값 · !방 · !내계좌 · !성적 · !업그레이드",
            "Commands: !analyze stock · !whale stock · !compare stock stock · !news stock · !alert stock price · !room · !account · !record · !upgrade",
            "使えるコマンド: !分析 銘柄 · !仕手 銘柄 · !比較 銘柄 銘柄 · !ニュース 銘柄 · !通知 銘柄 値段 · !部屋 · !口座 · !成績 · !アップグレード"));
    }

    // ── 장 시작 전 브리핑 ──
    /// <summary>날마다 장이 열리기 전(08:30부터)에 한 번, 오늘 볼 종목 셋을 올린다. 기사가 난 종목, 거래가 붙은 종목, 크게 움직인 종목 순이다.</summary>
    private void TickAiBriefing(double now)
    {
        if (day.Phase != MarketPhase.PreOpen || DawnAi.BriefDay == day.Day || day.ClockMinute < 510 || StockManager.Instance == null || NpcLeadingRoom.Instance == null || GetRoom(NpcLeadingRoom.RoomId) == null) { return; }
        DawnAi.BriefDay = day.Day;
        PostAiBriefing(NpcLeadingRoom.RoomId);
    }
    public bool PostAiBriefing(int room)
    {
        double now = SocialTime.Seconds;
        var ranked = new List<(Stock stock, float weight, string why)>();
        foreach (var stock in StockManager.Instance.stocks)
        {
            if (stock == null || !stock.IsListed || stock.IsDelisted || stock.currentPrice <= 0m) { continue; }
            decimal move = DayMove(stock); float heat = StockInterestManager.Instance == null ? 0f : StockInterestManager.Instance.Heat(stock); var article = LatestNews(stock);
            bool fresh = article != null && SessionClock.Time - article.publishedAt < 600 && SessionClock.Time >= article.publishedAt;
            float weight = (fresh ? 4f : article != null ? 1.5f : 0f) + heat + Mathf.Abs((float)move) * .6f + (float)(stock.symbol.GetHashCode() & 255) / 2550f;
            string why = fresh ? TalkWord("새 기사", "fresh news", "新着記事") : heat >= 3f ? TalkWord("거래 붙음", "heavy volume", "出来高増") : Math.Abs(move) >= 1m ? AiPercent(move) : article != null ? TalkWord("기사 있음", "in the news", "記事あり") : TalkWord("조용함", "quiet", "静か");
            ranked.Add((stock, weight, why));
        }
        if (ranked.Count == 0) { return false; }
        ranked.Sort((a, b) => b.weight.CompareTo(a.weight));
        int shown = Math.Min(3, ranked.Count);
        var card = AiCard("brief", TalkWord("오늘 볼 종목", "Today's watchlist", "本日の注目銘柄"), 50, "D" + day.Day, TalkWord("장 전", "Pre-open", "寄り前"), 3, 170);
        var parts = new List<string>();
        for (int i = 0; i < shown; i++)
        {
            decimal move = DayMove(ranked[i].stock);
            AiRow(card, i, AiName(ranked[i].stock), AiPrice(ranked[i].stock.currentPrice), Math.Sign(move));
            parts.Add(AiName(ranked[i].stock) + "(" + ranked[i].why + ")");
        }
        string text = TalkWord("장 시작 전 브리핑입니다. 오늘 볼 종목: " + string.Join(", ", parts) + "." + AiAdvice, "Pre-open briefing. On watch today: " + string.Join(", ", parts) + "." + AiAdvice,
            "寄り前ブリーフィングです。本日の注目: " + string.Join("、", parts) + "。" + AiAdvice);
        long id = PostAnalyst(room, text, card, 0);
        AiReports++;
        TraceTalk("    > AI[brief]: " + text);
        Plan(room, "aiReact.brief", ranked[0].stock, id, now + 2.0, 0, 2, .6f);
        return true;
    }
}
