using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 시세 상황 판정과 언어별 반응 대사의 점검. 플레이 모드 없이 돈다.
//
// 지어낸 가격 흐름을 판정에 넣어 보고(ChatSituationSim.Run: 게임이 쓰는 것과 같은 계산), 그 상황에서
//   - 어떤 상황으로 읽혔는가(지나온 상황들과 마지막 상황)
//   - 어떤 대사 묶음이 열려 있는가(폭락 묶음이 엉뚱한 데 열려 있지 않은가)
//   - 세 처지(수익 중인 보유자, 물린 보유자, 구경꾼) × 세 언어(ko, en, ja)로 실제 어떤 말이 나오는가(세 줄씩)
//   - 말맛(ChatVoice.cs)을 입힌 뒤의 모습: 언어마다 견본 40줄과 맞장구, 그리고 모든 문장을 여러 버릇으로 입혀 본 전수 검사
//     (빈 줄·남은 빈칸·겹친 말끝이 없는가, 잔잔한 상황에 긴 ㅋ·ㄷ·ｗ 줄이 붙지 않는가)
// 를 Validation/ChatSituation/report.txt에 적는다. 하나라도 어긋나면 종료 코드 1.
//
// 배치 실행:
//   Unity.exe -batchmode -nographics -projectPath "<프로젝트>" -executeMethod ChatSituationCheck.RunBatch -logFile -
// 에디터에서는 메뉴 DonDon/Chat/Situation check.
public static class ChatSituationCheck
{
    private sealed class Case
    {
        public string name, note;
        public decimal reference = 10000m, profitCost, lossCost;
        public (double seconds, decimal price)[] path;
        /// <summary>어긋난 점을 한 줄로 돌려준다. 괜찮으면 null.</summary>
        public Func<ChatSituationSim.Result, string> verify;
    }

    private static readonly string[] Languages = { "ko", "en", "ja" };
    private static readonly Dictionary<string, string> StockNames = new() { ["ko"] = "산삼전자", ["en"] = "Sansam", ["ja"] = "サンサム電子" };
    private static readonly string[] DoubledPercent = { "{dayPct}%", "{fromHigh}%", "{fromLow}%", "{retrace}%", "{gainLeft}%", "{highPct}%", "{lowPct}%" };
    private static readonly HashSet<string> MarketSlots = new(StringComparer.Ordinal)
    { "stockName", "price", "dayPct", "dayTalk", "multTalk", "bagger", "fromHigh", "fromLow", "retrace", "gainLeft", "dayHigh", "dayLow", "close", "highPct", "lowPct" };

    private static bool Has(ChatSituationSim.Result result, ChatSituation kind)
    {
        foreach (var step in result.timeline) { if (step.read.kind == kind) { return true; } }
        return false;
    }
    private static string Doom(ChatSituationSim.Result result)
    {
        foreach (string key in result.keys) { if (ChatSituationRules.DoomKey(key)) { return key; } }
        return null;
    }
    private static string Euphoria(ChatSituationSim.Result result)
    {
        foreach (string key in result.keys) { if (ChatSituationRules.EuphoriaKey(key)) { return key; } }
        return null;
    }
    private static bool Open(ChatSituationSim.Result result, string key) => result.keys.Contains(key);

    private static List<Case> Cases() => new()
    {
        new Case
        {
            name = "monsterDip", note = "+500%까지 갔다가 6초 만에 +490%로 내려옴. 오늘 번 것의 2%를 돌려줬을 뿐이다.",
            path = new[] { (0d, 10000m), (600d, 60000m), (20d, 60000m), (6d, 59000m), (60d, 59000m) }, profitCost = 11000m, lossCost = 59800m,
            verify = r => r.final.kind != ChatSituation.MonsterDip && r.final.kind != ChatSituation.MonsterRally ? "마지막 상황이 " + r.final.kind + "다(MonsterDip이어야 한다)" :
                Has(r, ChatSituation.Crash) || Has(r, ChatSituation.Capitulation) || Has(r, ChatSituation.RoundTrip) || Has(r, ChatSituation.Pullback) ? "도중에 폭락·왕복·눌림으로 읽혔다: " + r.Trail() :
                Doom(r) != null ? "폭락 묶음이 열려 있다: " + Doom(r) :
                Open(r, "twitch.spill.fuss") || Open(r, "twitch.spill.doubt") ? "잔밀림 호들갑이 열려 있다" :
                Open(r, "event.PriceFall.panic") ? "급락 사건이 공포 쪽으로 열려 있다" : null
        },
        new Case
        {
            name = "monsterSlide", note = "+500%까지 갔다가 5분에 걸쳐 +250%로 밀림. 오른 폭의 절반을 돌려줬다.",
            path = new[] { (0d, 10000m), (600d, 60000m), (300d, 35000m), (120d, 35000m) }, profitCost = 11000m, lossCost = 55000m,
            verify = r => r.final.kind != ChatSituation.Pullback && r.final.kind != ChatSituation.Crash && r.final.kind != ChatSituation.RoundTrip ? "마지막 상황이 " + r.final.kind + "다(Pullback·Crash·RoundTrip 가운데 하나여야 한다)" :
                !Has(r, ChatSituation.MonsterRally) ? "오르는 동안 MonsterRally로 읽히지 않았다: " + r.Trail() : null
        },
        new Case
        {
            name = "roundTrip", note = "+30%까지 갔다가 +5%로 돌아옴. 오른 폭의 83%를 돌려줬다.",
            path = new[] { (0d, 10000m), (300d, 13000m), (400d, 10500m), (150d, 10500m) }, profitCost = 10000m, lossCost = 12800m,
            verify = r => r.final.kind != ChatSituation.RoundTrip ? "마지막 상황이 " + r.final.kind + "다(RoundTrip이어야 한다)" : Euphoria(r) != null ? "환호 묶음이 열려 있다: " + Euphoria(r) : null
        },
        new Case
        {
            name = "flatNoise", note = "하루 종일 ±1% 안에서 출렁임. 소리칠 일이 없다.",
            path = new[] { (0d, 10000m), (60d, 10080m), (60d, 9930m), (60d, 10060m), (60d, 9950m), (60d, 10020m) }, profitCost = 9000m, lossCost = 11500m,
            verify = r => !ChatSituationRules.Still(r.final.kind) ? "마지막 상황이 " + r.final.kind + "다(Quiet·Flat이어야 한다)" :
                r.timeline.Exists(step => !ChatSituationRules.Still(step.read.kind)) ? "잔움직임이 말할 거리로 읽혔다: " + r.Trail() :
                Doom(r) != null ? "폭락 묶음이 열려 있다: " + Doom(r) : Euphoria(r) != null ? "환호 묶음이 열려 있다: " + Euphoria(r) :
                Open(r, "event.PriceFall.calm") || Open(r, "event.PriceRise") ? "가격 사건이 열려 있다" : null
        },
        new Case
        {
            name = "capitulationBounce", note = "−35%까지 무너진 뒤 저점에서 8% 튐. 빠진 폭의 15%만 되찾았다.",
            path = new[] { (0d, 10000m), (400d, 6500m), (40d, 6500m), (40d, 7020m), (20d, 7020m) }, profitCost = 6000m, lossCost = 9500m,
            verify = r => !Has(r, ChatSituation.Capitulation) ? "무너지는 동안 Capitulation으로 읽히지 않았다: " + r.Trail() :
                r.final.kind != ChatSituation.DeadCatBounce ? "마지막 상황이 " + r.final.kind + "다(DeadCatBounce여야 한다)" :
                Euphoria(r) != null ? "환호 묶음이 열려 있다: " + Euphoria(r) : Open(r, "twitch.pop.fuss") || Open(r, "twitch.pop.fomo") ? "잔반등 호들갑이 열려 있다" : null
        },
        new Case
        {
            name = "slowBleed", note = "장 내내 천천히 −12%까지 흘러내림.",
            path = new[] { (0d, 10000m), (800d, 8800m) }, profitCost = 8000m, lossCost = 10000m,
            verify = r => r.final.kind != ChatSituation.SlowBleed ? "마지막 상황이 " + r.final.kind + "다(SlowBleed여야 한다)" :
                Has(r, ChatSituation.Crash) || Has(r, ChatSituation.Capitulation) ? "천천히 내렸는데 폭락·투매로 읽혔다: " + r.Trail() : Euphoria(r) != null ? "환호 묶음이 열려 있다: " + Euphoria(r) : null
        },
        new Case
        {
            name = "breakout", note = "+10%에서 2분 반 버티다가 당일 고가를 넘어 +13%로.",
            path = new[] { (0d, 10000m), (100d, 11000m), (150d, 10950m), (30d, 11300m) }, profitCost = 10200m, lossCost = 11400m,
            verify = r => r.final.kind != ChatSituation.Breakout ? "마지막 상황이 " + r.final.kind + "다(Breakout이어야 한다): " + r.Trail() : Doom(r) != null ? "폭락 묶음이 열려 있다: " + Doom(r) : null
        },
        new Case
        {
            name = "limitUp", note = "60초 만에 +25%. 상한가처럼 솟음.",
            path = new[] { (0d, 10000m), (60d, 12500m) }, profitCost = 10100m, lossCost = 14000m,
            verify = r => r.final.kind != ChatSituation.Surge ? "마지막 상황이 " + r.final.kind + "다(Surge여야 한다)" : Doom(r) != null ? "폭락 묶음이 열려 있다: " + Doom(r) : !Open(r, "hype.surge") ? "급등 폭주가 닫혀 있다" : null
        },
        new Case
        {
            name = "tickInBigDay", note = "+80%인 날, 6초 사이에 1% 밀림.",
            path = new[] { (0d, 10000m), (500d, 18000m), (60d, 18000m), (6d, 17820m), (10d, 17820m) }, profitCost = 10500m, lossCost = 17950m,
            verify = r => r.final.kind != ChatSituation.DriftUp ? "마지막 상황이 " + r.final.kind + "다(DriftUp이어야 한다)" :
                Doom(r) != null ? "폭락 묶음이 열려 있다: " + Doom(r) : Open(r, "twitch.spill.fuss") || Open(r, "twitch.spill.doubt") ? "잔밀림 호들갑이 열려 있다" :
                !Open(r, "twitch.spill.calm") ? "느긋한 잔밀림 대사가 닫혀 있다" : null
        }
    };

    [MenuItem("DonDon/Chat/Situation check")]
    private static void RunFromMenu()
    {
        bool passed = Run(out string path);
        if (passed) { Debug.Log("Chat situation check passed · " + path); } else { Debug.LogError("Chat situation check FAILED · " + path); }
    }

    /// <summary>배치 실행용. 보고서를 쓰고 에디터를 끝낸다(통과 0, 실패 1).</summary>
    public static void RunBatch()
    {
        bool passed;
        try { passed = Run(out string path); Debug.Log((passed ? "Chat situation check passed · " : "Chat situation check FAILED · ") + path); }
        catch (Exception error) { Debug.LogError("Chat situation check crashed: " + error); passed = false; }
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static bool Run(out string reportPath)
    {
        string chat = Path.Combine(Application.dataPath, "Resources", "Chat");
        string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Validation", "ChatSituation");
        Directory.CreateDirectory(directory);
        reportPath = Path.Combine(directory, "report.txt");
        var report = new StringBuilder(); var failures = new List<string>();
        report.AppendLine("Chat situation check · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine();

        // 1) 언어별 대사 파일을 디스크에서 바로 읽는다(플레이 모드도 Resources도 필요 없다).
        ChatReactionAssets.Reader = code =>
        {
            string file = Path.Combine(chat, "chat_market." + code + ".json");
            return File.Exists(file) ? File.ReadAllText(file, Encoding.UTF8) : null;
        };
        try
        {
            ChatReactionAssets.Reload();
            foreach (string problem in ChatReactionAssets.Problems) { failures.Add("대사 파일: " + problem); }
            AuditPools(report, failures);
            AuditDoubledPercent(chat, report, failures);
            VoiceSection(report, failures);

            // 2) 가격 흐름마다: 판정 → 열린 묶음 → 처지 × 언어별 대사.
            foreach (var test in Cases())
            {
                report.AppendLine("════════════════════════════════════════════════════════════");
                report.AppendLine("■ " + test.name + " — " + test.note);
                var path = new StringBuilder();
                foreach (var point in test.path) { path.Append(path.Length == 0 ? "" : " → ").Append(point.seconds.ToString("0")).Append("s ").Append(InvestorSociety.Money(point.price)); }
                report.AppendLine("  기준가 " + InvestorSociety.Money(test.reference) + " · 흐름 " + path);
                var stances = new (string label, decimal? cost)[] { ("수익 중인 보유자(평단 " + InvestorSociety.Money(test.profitCost) + ")", test.profitCost), ("물린 보유자(평단 " + InvestorSociety.Money(test.lossCost) + ")", test.lossCost), ("구경꾼", null) };
                for (int s = 0; s < stances.Length; s++)
                {
                    var result = ChatSituationSim.Run(test.reference, test.path, stances[s].cost);
                    if (s == 0)
                    {
                        var read = result.final;
                        report.AppendLine("  지나온 상황: " + result.Trail());
                        report.AppendLine("  마지막 상황: " + read.kind + " · 전일 대비 " + ChatSituationMath.SignedPercent(read.dayPct) + " · 당일 고가 " + ChatSituationMath.SignedPercent(read.highPct) +
                            " · 저가 " + ChatSituationMath.SignedPercent(read.lowPct) + " · 고점 대비 " + ChatSituationMath.SignedPercent(read.fromHigh) + " · 저점 대비 " + ChatSituationMath.SignedPercent(read.fromLow) +
                            " · 되돌림 " + ChatSituationMath.SharePercent(read.retrace) + " · 회복 " + ChatSituationMath.SharePercent(read.recovery) +
                            " · 30초 " + ChatSituationMath.SignedPercent(read.r30) + " · 2분 " + ChatSituationMath.SignedPercent(read.r120));
                        report.AppendLine("  예전 대화 틀의 상태: " + (ChatSituationRules.LegacyState(read.kind) ?? "(없음)"));
                        string verdict = test.verify(result);
                        report.AppendLine("  판정: " + (verdict == null ? "통과" : "실패 — " + verdict));
                        if (verdict != null) { failures.Add(test.name + ": " + verdict); }
                    }
                    report.AppendLine();
                    report.AppendLine("  ▷ " + stances[s].label + " · 처지 " + result.view + " · 묶음 market." + result.final.kind + "." + ChatSituationMath.StanceKey(result.stance));
                    report.AppendLine("    열린 묶음: " + string.Join(", ", result.keys));
                    foreach (string code in Languages)
                    {
                        var engine = ChatReactionAssets.Get(code);
                        if (engine == null) { report.AppendLine("    [" + code + "] (엔진 없음: 예전 번역 대사로 말한다)"); continue; }
                        if (result.final.kind == ChatSituation.Quiet) { report.AppendLine("    [" + code + "] (말하지 않는다)"); continue; }
                        report.AppendLine("    [" + code + "] 말할 확률 " + engine.TalkChance(result.final.kind, result.stance).ToString("0.00"));
                        var lines = Samples(engine, result, StockNames[code], CompanyBusinessSeed(test.name + code + s), failures, test.name);
                        if (lines.Count < 3) { failures.Add(test.name + " " + code + " " + result.stance + ": 고를 수 있는 대사가 세 줄이 안 된다"); }
                        foreach (string line in lines) { report.AppendLine("        " + line); }
                    }
                }
                report.AppendLine();
            }
        }
        finally
        {
            // 점검이 끝나면 게임이 다시 Resources에서 읽게 되돌린다.
            ChatReactionAssets.Reset();
        }

        report.AppendLine("════════════════════════════════════════════════════════════");
        if (failures.Count == 0) { report.AppendLine("RESULT: PASS"); }
        else
        {
            report.AppendLine("RESULT: FAIL (" + failures.Count + ")");
            foreach (string failure in failures) { report.AppendLine("  - " + failure); }
        }
        File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
        return failures.Count == 0;
    }

    private static int CompanyBusinessSeed(string text) => unchecked((int)CompanyBusiness.Hash(text));

    /// <summary>그 상황·처지에서 실제로 나올 수 있는 말 세 줄(서로 다른 문장, 서로 다른 말버릇).</summary>
    private static List<string> Samples(ChatReactionEngine engine, ChatSituationSim.Result result, string stockName, int seed, List<string> failures, string caseName)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal) { ["stockName"] = stockName, ["price"] = InvestorSociety.Money(result.final.price) };
        engine.FillSlots(result.final, slots);
        var random = new System.Random(seed); var used = new HashSet<string>(StringComparer.Ordinal); var lines = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            string line = engine.Pick(result.final.kind, result.view, result.stance, slots, random.NextDouble, used.Contains, out string key, out int number);
            if (line == null || !used.Add(line)) { break; }
            string dressed = engine.Dress(line, result.final.kind, unchecked((uint)(seed * 31 + i * 7919)), random.NextDouble);
            // 게임에서도 종목 이름이 없는 대사 앞에는 이름을 붙인다(여러 종목 얘기가 섞인 방에서 무엇을 두고 하는 말인지 알 수 있게).
            if (dressed.IndexOf("{stockName}", StringComparison.Ordinal) < 0) { dressed = "{stockName} " + dressed; }
            string said = ChatHypeAssets.Fill(dressed, slots);
            if (said.IndexOf('{') >= 0 || said.IndexOf("%%", StringComparison.Ordinal) >= 0) { failures.Add(caseName + " " + engine.Code + ": 빈칸이 남았거나 %가 겹쳤다 — " + said); }
            lines.Add(said + "    (" + key + "." + number + ")");
        }
        return lines;
    }

    /// <summary>모든 상황 × 처지에 대사가 여덟 줄 이상 있는가, 모르는 빈칸은 없는가.</summary>
    private static void AuditPools(StringBuilder report, List<string> failures)
    {
        report.AppendLine("대사 묶음(상황 × 처지별 줄 수)");
        foreach (string code in Languages)
        {
            var engine = ChatReactionAssets.Get(code);
            if (engine == null) { failures.Add(code + ": 시세 반응 엔진이 없다(chat_market." + code + ".json)"); continue; }
            int total = 0; var row = new StringBuilder("  [" + code + "]");
            foreach (ChatSituation kind in Enum.GetValues(typeof(ChatSituation)))
            {
                if (kind == ChatSituation.Quiet) { continue; }
                row.Append(" " + kind + " ");
                foreach (string stance in new[] { "gain", "loss", "watch", "deep" })
                {
                    int count = engine.Lines(kind + "." + stance).Length; total += count;
                    if (stance != "deep" || count > 0) { row.Append(count).Append(stance == "deep" ? "(deep)" : "/"); }
                    if (stance != "deep" && count < 8) { failures.Add(code + " " + kind + "." + stance + ": 대사가 " + count + "줄뿐이다(8줄 이상이어야 한다)"); }
                }
            }
            foreach (string key in engine.Keys)
            {
                foreach (string text in engine.Lines(key))
                {
                    if (key.StartsWith("close.", StringComparison.Ordinal)) { total++; }
                    for (int open = text.IndexOf('{'); open >= 0; open = text.IndexOf('{', open + 1))
                    {
                        int close = text.IndexOf('}', open + 1);
                        string slot = close < 0 ? text.Substring(open) : text.Substring(open + 1, close - open - 1);
                        if (!MarketSlots.Contains(slot)) { failures.Add(code + " " + key + ": 모르는 빈칸 {" + slot + "} — " + text); }
                    }
                }
            }
            report.AppendLine(row.ToString());
            report.AppendLine("      모두 " + total + "줄");
        }
        report.AppendLine();
    }

    // ── 말맛(ChatVoice.cs) ──
    /// <summary>세기가 낮은 것부터 높은 것까지 고루 섞은 상황들. 견본은 이 순서로 돌아가며 뽑는다.</summary>
    private static readonly ChatSituation[] VoiceKinds =
    {
        ChatSituation.Flat, ChatSituation.DriftUp, ChatSituation.DriftDown, ChatSituation.MonsterDip,
        ChatSituation.Pullback, ChatSituation.SlowBleed, ChatSituation.Recovery, ChatSituation.DeadCatBounce, ChatSituation.RoundTrip,
        ChatSituation.Breakout, ChatSituation.Surge, ChatSituation.Crash, ChatSituation.MonsterRally, ChatSituation.Capitulation
    };
    /// <summary>그 상황다운 하루를 지어낸다(빈칸에 넣을 수치용). 판정은 거치지 않는다.</summary>
    private static ChatSituationRead VoiceRead(ChatSituation kind)
    {
        (decimal day, decimal high, decimal low) = kind switch
        {
            ChatSituation.Flat => (.3m, .9m, -.7m), ChatSituation.DriftUp => (3.2m, 3.4m, -.4m), ChatSituation.DriftDown => (-2.6m, .5m, -2.9m),
            ChatSituation.MonsterDip => (380m, 400m, -1m), ChatSituation.Pullback => (13m, 20m, -1m), ChatSituation.SlowBleed => (-11m, .5m, -11.5m),
            ChatSituation.Recovery => (-3m, 1m, -15m), ChatSituation.DeadCatBounce => (-28m, 0m, -35m), ChatSituation.RoundTrip => (4m, 30m, -1m),
            ChatSituation.Breakout => (13m, 13m, -1m), ChatSituation.Surge => (24m, 24m, 0m), ChatSituation.Crash => (-17m, 2m, -18m),
            ChatSituation.MonsterRally => (490m, 490m, 0m), _ => (-34m, 0m, -34m)
        };
        decimal reference = 10000m, price = reference * (1m + day / 100m), top = reference * (1m + high / 100m), bottom = reference * (1m + low / 100m);
        return new ChatSituationRead
        {
            kind = kind, valid = true, reference = reference, price = price, high = top, low = bottom, dayPct = day, highPct = high, lowPct = low,
            fromHigh = (price / top - 1m) * 100m, fromLow = (price / bottom - 1m) * 100m, multiple = price / reference,
            retrace = top > reference ? (top - price) / (top - reference) : 0m, recovery = bottom < reference ? (price - bottom) / (reference - bottom) : 0m
        };
    }
    private static Dictionary<string, string> VoiceSlots(ChatReactionEngine engine, ChatSituationRead read, string stockName)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal) { ["stockName"] = stockName, ["price"] = InvestorSociety.Money(read.price) };
        engine.FillSlots(read, slots);
        return slots;
    }

    /// <summary>
    /// 대사 파일의 문장에 말맛을 입힌 뒤의 모습. 게임이 쓰는 것과 같은 길(ChatReactionEngine.Dress)을 거친다.
    ///   1) 언어마다 40줄을 여러 상황·세기에 걸쳐 적는다(사람이 읽고 말투를 판단하라고). 원문과 달라진 줄에는 원문을 같이 적는다.
    ///   2) 모든 상황 × 처지의 모든 문장을 버릇 여덟 가지로 입혀 보고 따진다: 빈 줄이 없는가, 빈칸이 남지 않았는가,
    ///      말끝이 겹치지 않았는가("ㄷㄷ ㄷㄷ", "lol lol"), 잔잔한 상황에 긴 ㅋ·ㄷ·ｗ 줄이나 길게 늘인 글자가 붙지 않았는가, 씨앗이 같으면 같은 말이 나오는가.
    ///   3) 맞장구를 갈래 × 세기별로 적는다.
    /// </summary>
    private static void VoiceSection(StringBuilder report, List<string> failures)
    {
        report.AppendLine("════════════════════════════════════════════════════════════");
        report.AppendLine("■ 말맛(런타임 변주) — 대사 파일의 문장에 말머리·끼우는 말·ㅋ/ㄷ/ｗ 길이·글자 늘이기·말끝 버릇을 입힌 뒤의 모습");
        report.AppendLine("  세기: 0 잔잔함 · 1 눈에 띔 · 2 큰일 · 3 난리 (상황이 정한다). [ ] 안은 그 줄의 세기, 화살표 뒤는 파일에 적힌 원문이다.");
        report.AppendLine("  게임에서는 종목 이름이 없는 말 앞에 이름이 붙는다. 여기서는 말투만 보려고 붙이지 않았다.");
        report.AppendLine();
        foreach (string code in Languages)
        {
            var engine = ChatReactionAssets.Get(code);
            if (engine == null) { continue; }
            var voice = engine.Voice;
            if (voice == null || voice.openerChance == null || voice.openerChance.Length == 0) { failures.Add(code + ": 말맛 규칙이 없다(chat_market." + code + ".json의 voice)"); continue; }
            // 규칙 자체가 잔잔한 날에 긴 줄을 허락하면 안 된다.
            if (!string.IsNullOrEmpty(voice.runChars) && (voice.runMax == null || voice.runMax.Length < 4 || voice.runMax[0] > 4 || voice.runMax[0] > voice.runMax[3]))
            { failures.Add(code + ": 잔잔한 상황의 줄 길이 윗선(runMax[0])이 4를 넘거나 난리 난 상황보다 길다"); }
            if (voice.stretch != null && voice.stretch.Length > 0 && (voice.stretchMax == null || voice.stretchMax.Length < 4 || voice.stretchMax[0] > 1))
            { failures.Add(code + ": 잔잔한 상황에서 글자를 두 개 넘게 늘일 수 있다(stretchMax[0])"); }
            if (voice.backChance > 0 && (voice.backGapSec < 5 || voice.backMaxPerRun > 4)) { failures.Add(code + ": 맞장구가 너무 잦다(backGapSec " + voice.backGapSec + ", backMaxPerRun " + voice.backMaxPerRun + ")"); }
            report.AppendLine("  ── [" + code + "] 규칙 ──");
            report.AppendLine("    말머리 확률(세기별) " + Numbers(voice.openerChance) + " · 끼우는 말 " + voice.softenerChance.ToString("0.##") + " · 말끝 버릇 " + engine.Style.tailChance.ToString("0.##") +
                (string.IsNullOrEmpty(voice.runChars) ? "" : " · 늘여 치는 글자 \"" + voice.runChars + "\" 길이 " + Numbers(voice.runMin) + " ~ " + Numbers(voice.runMax) + "(다시 정할 확률 " + voice.runVaryChance.ToString("0.##") + ")") +
                (voice.stretch == null || voice.stretch.Length == 0 ? "" : " · 글자 늘이기 " + Numbers(voice.stretchChance) + " 최대 +" + Numbers(voice.stretchMax) + " · 대문자 " + Numbers(voice.capsChance)) +
                (voice.endings == null || voice.endings.Length == 0 ? "" : " · 말끝 바꾸기 " + voice.endingChance.ToString("0.##")) +
                " · 맞장구 " + voice.backChance.ToString("0.##") + "(틈 " + voice.backGapSec.ToString("0") + "초, 폭주 한 번에 " + voice.backMaxPerRun + "번까지)");

            // 1) 견본 40줄.
            report.AppendLine("  ── [" + code + "] 견본 40줄 ──");
            var random = new System.Random(CompanyBusinessSeed("voice" + code)); var shown = new HashSet<string>(StringComparer.Ordinal); int printed = 0;
            var stances = new[] { ChatStance.Watch, ChatStance.Gain, ChatStance.Loss };
            for (int round = 0; printed < 40 && round < 12; round++)
            {
                for (int k = 0; k < VoiceKinds.Length && printed < 40; k++)
                {
                    var kind = VoiceKinds[k]; var stance = stances[(round + k) % 3];
                    var view = stance == ChatStance.Gain ? ChatSpeakerView.Profit : stance == ChatStance.Loss ? ChatSpeakerView.Loss : ChatSpeakerView.Watcher;
                    var slots = VoiceSlots(engine, VoiceRead(kind), StockNames[code]);
                    string line = engine.Pick(kind, view, stance, slots, random.NextDouble, shown.Contains, out string key, out int number);
                    if (line == null || !shown.Add(line)) { continue; }
                    string said = ChatHypeAssets.Fill(engine.Dress(line, kind, unchecked((uint)random.Next()), random.NextDouble), slots), plain = ChatHypeAssets.Fill(line, slots);
                    report.AppendLine("      [" + ChatVoice.Level(voice, kind) + "] " + said + "    (" + key + "." + number + (said == plain ? "" : " ← " + plain) + ")");
                    printed++;
                }
            }
            if (printed < 40) { failures.Add(code + ": 말맛 견본이 " + printed + "줄뿐이다(40줄이어야 한다)"); }

            // 2) 전수 검사.
            int looked = 0, changed = 0, problems = 0, boostedTotal = 0; var tails = new List<string>(engine.Tails);
            var boosted = new List<string>(); var boostedWords = new HashSet<string>(StringComparer.Ordinal);
            foreach (ChatSituation kind in Enum.GetValues(typeof(ChatSituation)))
            {
                if (kind == ChatSituation.Quiet) { continue; }
                var slots = VoiceSlots(engine, VoiceRead(kind), StockNames[code]); int level = ChatVoice.Level(voice, kind);
                foreach (string stance in new[] { "gain", "loss", "watch", "deep" })
                {
                    foreach (string line in engine.Lines(kind + "." + stance))
                    {
                        if (!ChatReactionEngine.Fillable(line, slots)) { continue; }
                        string plain = ChatHypeAssets.Fill(line, slots);
                        for (int h = 0; h < 8; h++)
                        {
                            int seed = CompanyBusinessSeed(code + "|" + kind + "|" + line + "|" + h); uint habit = unchecked((uint)(seed * 2654435761u + h * 40503u));
                            string said = ChatHypeAssets.Fill(engine.Dress(line, kind, habit, new System.Random(seed).NextDouble), slots);
                            looked++; if (said != plain) { changed++; }
                            // 힘주는 말머리("개")가 붙은 문장을 따로 모아 둔다(어떤 낱말에 붙었는지 눈으로 본다).
                            if (!string.IsNullOrEmpty(voice.boostPrefix) && said != plain)
                            {
                                foreach (string word in said.Split(' '))
                                {
                                    if (word.Length > voice.boostPrefix.Length + 1 && word.StartsWith(voice.boostPrefix, StringComparison.Ordinal) && plain.IndexOf(word, StringComparison.Ordinal) < 0
                                        && plain.IndexOf(word.Substring(voice.boostPrefix.Length), StringComparison.Ordinal) >= 0)
                                    { boostedTotal++; if (boostedWords.Add(word) && boosted.Count < 70) { boosted.Add("[" + level + "] " + said + "    ← " + plain); } break; }
                                }
                            }
                            string problem = ChatVoice.Audit(said, voice, level, tails);
                            // 씨앗과 버릇이 같으면 같은 말이 나와야 한다(저장·검증 실행에서 같은 대화를 다시 볼 수 있게).
                            if (problem == null && h == 0 && said != ChatHypeAssets.Fill(engine.Dress(line, kind, habit, new System.Random(seed).NextDouble), slots)) { problem = "같은 씨앗인데 다른 말이 나왔다"; }
                            if (problem != null && ++problems <= 12) { failures.Add(code + " market." + kind + "." + stance + " 말맛: " + problem + " — " + said + " (원문: " + line + ")"); }
                        }
                    }
                }
            }
            if (problems > 12) { failures.Add(code + ": 말맛 문제가 " + (problems - 12) + "건 더 있다"); }
            report.AppendLine("  ── [" + code + "] 전수 검사: " + looked + "줄(모든 상황 × 처지의 문장 × 버릇 8가지) · 원문과 달라진 줄 " + changed + "(" + (looked == 0 ? 0 : changed * 100 / looked) + "%) · 문제 " + problems + "건 ──");
            if (!string.IsNullOrEmpty(voice.boostPrefix))
            {
                report.AppendLine("  ── [" + code + "] 힘주는 말머리 \"" + voice.boostPrefix + "\": 붙은 줄 " + boostedTotal + "(" + (looked == 0 ? 0 : boostedTotal * 1000 / looked / 10f).ToString("0.#") + "%) · 붙은 낱말 " + boostedWords.Count + "가지 · 낱말마다 한 줄씩 ──");
                foreach (string row in boosted) { report.AppendLine("      " + row); }
            }

            // 3) 맞장구.
            report.AppendLine("  ── [" + code + "] 맞장구(세기 0 → 3) ──");
            var backRandom = new System.Random(CompanyBusinessSeed("back" + code));
            foreach (string mood in new[] { "agree", "laugh", "awe", "sad" })
            {
                var row = new StringBuilder("      " + mood.PadRight(6));
                for (int level = 0; level <= 3; level++)
                {
                    var used = new HashSet<string>(StringComparer.Ordinal);
                    for (int n = 0; n < 3; n++)
                    {
                        string back = ChatVoice.Back(voice, mood, level, backRandom.NextDouble, used.Contains);
                        string problem = ChatVoice.Audit(back, voice, level, tails);
                        if (problem != null) { failures.Add(code + " 맞장구 " + mood + ": " + problem + " — " + back); }
                        if (back != null) { used.Add(back); row.Append(n == 0 ? (level == 0 ? "" : "  |  ") : " · ").Append(back); }
                    }
                }
                report.AppendLine(row.ToString());
            }
            report.AppendLine();
        }
    }
    private static string Numbers(double[] values) => values == null ? "-" : string.Join("/", Array.ConvertAll(values, value => value.ToString("0.##")));
    private static string Numbers(int[] values) => values == null ? "-" : string.Join("/", values);

    /// <summary>이미 %가 붙어서 들어오는 빈칸 뒤에 대사가 %를 또 붙이지 않았는가. Chat 폴더의 모든 대사 파일을 본다.</summary>
    private static void AuditDoubledPercent(string chat, StringBuilder report, List<string> failures)
    {
        int files = 0;
        foreach (string file in Directory.GetFiles(chat, "*.json"))
        {
            files++;
            string text = File.ReadAllText(file, Encoding.UTF8);
            foreach (string pattern in DoubledPercent)
            {
                int at = text.IndexOf(pattern, StringComparison.Ordinal);
                if (at >= 0) { failures.Add(Path.GetFileName(file) + ": " + pattern + " (%가 두 번 붙는다) — …" + text.Substring(Math.Max(0, at - 24), Math.Min(text.Length - Math.Max(0, at - 24), 60)).Replace("\n", " ") + "…"); }
            }
        }
        report.AppendLine("% 겹침 검사: 대사 파일 " + files + "개");
        report.AppendLine();
    }
}
