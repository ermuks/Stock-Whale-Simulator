using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 인증 카드 반응(금액 × 수익률)과 새벽 관찰실 대사의 점검. 플레이 모드 없이 돈다.
//
//   1) 인증 카드: 지어낸 카드(+1%·500돈, +1%·5,000만돈, +80%·3,000돈 …)를 게임과 같은 계산(ChatProofMath.Read)으로 읽어
//      금액 단·수익률 단·기분을 적고, 그 기분에서 세 언어로 실제 어떤 말이 나오는지 세 줄씩 적는다.
//      "500돈 카드에 놀라는 말이 달리지 않는가", "5,000만돈 카드에 금액이 작다고 놀리는 말이 달리지 않는가"를 여러 번 뽑아서 확인한다.
//      보는 사람의 재산에 따라 읽기가 달라지는지도 본다(부자는 덜 놀란다).
//      말맛(ChatVoice.cs)을 입힌 뒤의 견본도 언어마다 40줄 적고, 금액이 작은 카드에 긴 ㅋ·ㄷ 줄이 붙지 않는지, 위로·손실·새벽 관찰실의 말에 말머리나 대문자가 붙지 않는지 따진다.
//   2) 새벽 관찰실: 대사 묶음이 다 있는가, 묶음마다 줄 수는 넉넉한가, 값을 대는 문장의 몫은 충분한가, 호들갑(ㅋㅋ·ㄷㄷ·lol·草)이 섞이지 않았는가.
//      그리고 몇 가지 장면(오르는 날, 눌림, 흘러내림, 폭락, 반등, 횡보)에서 그 방에 실제로 뜰 대화를 언어마다 서른 줄 적는다(사람이 읽고 판단하라고).
// 결과는 Validation/ChatSituation/proof-report.txt. 하나라도 어긋나면 종료 코드 1.
//
// 배치 실행:
//   Unity.exe -batchmode -nographics -projectPath "<프로젝트>" -executeMethod ChatProofCheck.RunBatch -logFile -
// 에디터에서는 메뉴 DonDon/Chat/Proof and dawn check.
public static class ChatProofCheck
{
    private sealed class ProofCase
    {
        public string name, note;
        public bool holding;
        public decimal amount, percent, stake;
        /// <summary>보는 사람의 재산. 따로 적지 않으면 5,000만(중간쯤 되는 봇)으로 잡는다.</summary>
        public decimal viewer = 50_000_000m;
        public ChatProofMood expected;
        public bool noAwe, noTease;
    }

    private static readonly string[] Languages = { "ko", "en", "ja" };
    private static readonly Dictionary<string, string> StockNames = new() { ["ko"] = "산삼전자", ["en"] = "Sansam", ["ja"] = "サンサム電子" };
    private static readonly HashSet<string> ProofSlots = new(StringComparer.Ordinal) { "amount", "pct", "stake", "stockName" };
    private static readonly HashSet<string> DawnSlots = new(StringComparer.Ordinal) { "stockName", "price", "dayHigh", "dayLow", "mid", "support", "resist", "dayPct", "fromHigh", "fromLow", "avg", "myPct", "bidWall", "askWall" };
    /// <summary>새벽 관찰실 대사에 섞이면 안 되는 호들갑 표시.</summary>
    private static readonly string[] Noise = { "ㅋㅋ", "ㄷㄷ", "ㅠㅠ", "lol", "lmao", "!!", "ｗ", "草" };

    private static List<ProofCase> ProofCases() => new()
    {
        new ProofCase { name = "+1% · 500돈 실현", note = "푼돈. 놀라면 안 된다.", amount = 500m, percent = 1m, expected = ChatProofMood.TeaseSmall, noAwe = true },
        new ProofCase { name = "+1% · 5,000만돈 실현", note = "수익률은 얇은데 금액이 크다. 판돈에 놀란다. 금액이 작다고 놀리면 안 된다.", amount = 50_000_000m, percent = 1m, expected = ChatProofMood.StakeAwe, noTease = true },
        new ProofCase { name = "+80% · 3,000돈 실현", note = "수익률은 엄청난데 금액이 작다.", amount = 3_000m, percent = 80m, expected = ChatProofMood.PctNoSeed, noAwe = true },
        new ProofCase { name = "+80% · 4억돈 실현", note = "둘 다 크다. 충격.", amount = 400_000_000m, percent = 80m, expected = ChatProofMood.Shock, noTease = true },
        new ProofCase { name = "+8% · 300만돈 실현", note = "괜찮은 하루. 덤덤한 축하.", amount = 3_000_000m, percent = 8m, expected = ChatProofMood.Modest, noAwe = true },
        new ProofCase { name = "+25% · 3,000만돈 실현", note = "월급만큼 벌었다. 부러워한다.", amount = 30_000_000m, percent = 25m, expected = ChatProofMood.Envy, noTease = true },
        new ProofCase { name = "−2% · 800돈 실현", note = "푼돈 손실. 어깨를 으쓱한다.", amount = -800m, percent = -2m, expected = ChatProofMood.ShrugLoss, noAwe = true },
        new ProofCase { name = "−45% · 3억돈 실현", note = "큰 손실. 놀라거나 위로하거나 쓴웃음.", amount = -300_000_000m, percent = -45m, expected = ChatProofMood.Disaster, noTease = true },
        new ProofCase { name = "−60% · 4,000돈 실현", note = "손실률은 처참한데 금액은 작다.", amount = -4_000m, percent = -60m, expected = ChatProofMood.PctWreck, noAwe = true },
        new ProofCase { name = "−1.5% · 6,000만돈 실현", note = "손실률은 얇은데 금액이 크다.", amount = -60_000_000m, percent = -1.5m, expected = ChatProofMood.StakeOuch, noTease = true },
        new ProofCase { name = "−12% · 500만돈 실현", note = "아픈 손실.", amount = -5_000_000m, percent = -12m, expected = ChatProofMood.Sting, noAwe = false },
        new ProofCase { name = "보유 200만돈 · +0.5%", note = "평가익 1만돈. 5,000만을 굴리는 사람 눈에는 푼돈이다.", holding = true, amount = 10_000m, percent = .5m, stake = 2_000_000m, expected = ChatProofMood.TeaseSmall, noAwe = true },
        new ProofCase { name = "보유 200만돈 · +0.5%를 초보(재산 300만)가 봄", note = "같은 카드도 재산이 비슷한 사람에게는 밥값이다.", holding = true, amount = 10_000m, percent = .5m, stake = 2_000_000m, viewer = 3_000_000m, expected = ChatProofMood.Modest, noAwe = true },
        new ProofCase { name = "보유 30억돈 · +12%", note = "평가익 3.6억돈. 금액도 들고 있는 크기도 크다.", holding = true, amount = 360_000_000m, percent = 12m, stake = 3_000_000_000m, expected = ChatProofMood.Shock, noTease = true },
        new ProofCase { name = "보유 30억돈 · +0.2%", note = "평가익 600만돈뿐이지만 들고 있는 크기에 놀란다.", holding = true, amount = 6_000_000m, percent = .2m, stake = 3_000_000_000m, expected = ChatProofMood.StakeAwe, noTease = true },
        new ProofCase { name = "보유 5만돈 · +40%", note = "들고 있는 것 자체가 푼돈.", holding = true, amount = 20_000m, percent = 40m, stake = 50_000m, expected = ChatProofMood.PctNoSeed, noAwe = true },
        new ProofCase { name = "+1% · 5,000만돈을 큰손(재산 1,000억)이 봄", note = "부자는 덜 놀란다: 한 단 낮춰 읽는다. 그래도 푼돈 취급은 아니다.", amount = 50_000_000m, percent = 1m, viewer = 100_000_000_000m, expected = ChatProofMood.Modest, noTease = true },
        new ProofCase { name = "+20% · 800만돈을 가난한 봇(재산 300만)이 봄", note = "자기 재산보다 많은 돈이다: 한 단 높여 읽는다.", amount = 8_000_000m, percent = 20m, viewer = 3_000_000m, expected = ChatProofMood.Envy, noTease = true },
        new ProofCase { name = "+1% · 500돈을 가난한 봇(재산 400)이 봄", note = "푼돈은 누가 봐도 푼돈이다.", amount = 500m, percent = 1m, viewer = 400m, expected = ChatProofMood.TeaseSmall, noAwe = true }
    };

    // ── 새벽 관찰실 장면 ──
    private sealed class DawnStep
    {
        public string who; public SocialAct act; public SocialAct? parent; public bool self, bought, sold;
        /// <summary>0 수익 중인 보유자, 1 물린 보유자, 2 구경꾼.</summary>
        public int stance; public string view = "neutral", parentView;
    }
    private sealed class DawnScene
    {
        public string name, note; public decimal gainCost, lossCost;
        public (double seconds, decimal price)[] path; public ChatDawnBucket expected; public DawnStep[] steps;
    }
    private static DawnStep Step(string who, SocialAct act, int stance, string view = "neutral", SocialAct? parent = null, string parentView = null, bool self = false, bool bought = false, bool sold = false)
        => new() { who = who, act = act, stance = stance, view = view, parent = parent, parentView = parentView, self = self, bought = bought, sold = sold };

    private static List<DawnScene> DawnScenes() => new()
    {
        new DawnScene { name = "오르는 날", note = "5분에 걸쳐 +12%, 고점 근처", path = new[] { (0d, 10000m), (300d, 11200m), (60d, 11150m) }, gainCost = 10300m, lossCost = 11400m, expected = ChatDawnBucket.Up,
            steps = new[] { Step("A", SocialAct.REACT, 0, "bull"), Step("B", SocialAct.ASK, 2), Step("C", SocialAct.ANSWER, 0, "bull", SocialAct.ASK), Step("B", SocialAct.ASK, 2, "neutral", SocialAct.ANSWER, "bull"), Step("D", SocialAct.REBUT, 2, "bear", SocialAct.ANSWER, "bull") } },
        new DawnScene { name = "눌림", note = "+20%까지 갔다가 고점에서 6% 눌림", path = new[] { (0d, 10000m), (300d, 12000m), (200d, 11300m), (60d, 11300m) }, gainCost = 10400m, lossCost = 11800m, expected = ChatDawnBucket.Dip,
            steps = new[] { Step("A", SocialAct.REACT, 2), Step("B", SocialAct.REACT, 0, "bull", self: true, bought: true), Step("C", SocialAct.ASK, 2, "neutral", SocialAct.REACT, "bull"), Step("B", SocialAct.ANSWER, 0, "neutral", SocialAct.ASK), Step("D", SocialAct.AGREE, 2, "neutral", SocialAct.ANSWER, "neutral") } },
        new DawnScene { name = "흘러내리는 날", note = "장 내내 천천히 −12%", path = new[] { (0d, 10000m), (800d, 8800m) }, gainCost = 8000m, lossCost = 10000m, expected = ChatDawnBucket.Down,
            steps = new[] { Step("A", SocialAct.REACT, 1, "bear"), Step("B", SocialAct.COMFORT, 2, "neutral", SocialAct.REACT, "bear"), Step("C", SocialAct.DOUBT, 2, "bull"), Step("D", SocialAct.REBUT, 2, "bear", SocialAct.DOUBT, "bull"), Step("C", SocialAct.AGREE, 2, "bull", SocialAct.REBUT, "bear") } },
        new DawnScene { name = "폭락", note = "잠잠하다가 50초 사이에 −18%까지", path = new[] { (0d, 10000m), (200d, 9800m), (40d, 8300m), (10d, 8200m) }, gainCost = 7000m, lossCost = 9900m, expected = ChatDawnBucket.Break,
            steps = new[] { Step("A", SocialAct.REACT, 2), Step("B", SocialAct.REACT, 2, "bear", self: true, sold: true), Step("C", SocialAct.ASK, 2), Step("D", SocialAct.ANSWER, 2, "bear", SocialAct.ASK), Step("E", SocialAct.REACT, 1, "neutral") } },
        new DawnScene { name = "반등", note = "−35%까지 무너진 뒤 저점에서 8% 튐", path = new[] { (0d, 10000m), (400d, 6500m), (40d, 6500m), (40d, 7020m), (20d, 7020m) }, gainCost = 6600m, lossCost = 9500m, expected = ChatDawnBucket.Bounce,
            steps = new[] { Step("A", SocialAct.REACT, 0, "neutral"), Step("B", SocialAct.DOUBT, 2, "bear"), Step("C", SocialAct.REBUT, 2, "bull", SocialAct.DOUBT, "bear"), Step("B", SocialAct.ASK, 2, "bear", SocialAct.REBUT, "bull"), Step("C", SocialAct.ANSWER, 2, "bull", SocialAct.ASK) } },
        new DawnScene { name = "횡보", note = "하루 종일 ±1% 안", path = new[] { (0d, 10000m), (60d, 10080m), (60d, 9930m), (60d, 10060m), (60d, 9950m), (60d, 10020m) }, gainCost = 9000m, lossCost = 11500m, expected = ChatDawnBucket.Flat,
            steps = new[] { Step("A", SocialAct.REACT, 2), Step("B", SocialAct.ASK, 2), Step("C", SocialAct.ANSWER, 2, "neutral", SocialAct.ASK), Step("D", SocialAct.DOUBT, 2), Step("E", SocialAct.AGREE, 2, "neutral", SocialAct.ANSWER, "neutral") } }
    };

    [MenuItem("DonDon/Chat/Proof and dawn check")]
    private static void RunFromMenu()
    {
        bool passed = Run(out string path);
        if (passed) { Debug.Log("Chat proof check passed · " + path); } else { Debug.LogError("Chat proof check FAILED · " + path); }
    }

    /// <summary>배치 실행용. 보고서를 쓰고 에디터를 끝낸다(통과 0, 실패 1).</summary>
    public static void RunBatch()
    {
        bool passed;
        try { passed = Run(out string path); Debug.Log((passed ? "Chat proof check passed · " : "Chat proof check FAILED · ") + path); }
        catch (Exception error) { Debug.LogError("Chat proof check crashed: " + error); passed = false; }
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static int Seed(string text) => unchecked((int)CompanyBusiness.Hash(text));

    private static bool Run(out string reportPath)
    {
        string chat = Path.Combine(Application.dataPath, "Resources", "Chat");
        string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Validation", "ChatSituation");
        Directory.CreateDirectory(directory);
        reportPath = Path.Combine(directory, "proof-report.txt");
        var report = new StringBuilder(); var failures = new List<string>();
        report.AppendLine("Chat proof and dawn room check · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine();

        // 언어별 대사 파일을 디스크에서 바로 읽는다(플레이 모드도 Resources도 필요 없다).
        Func<string, Func<string, string>> reader = prefix => code =>
        {
            string file = Path.Combine(chat, prefix + "." + code + ".json");
            return File.Exists(file) ? File.ReadAllText(file, Encoding.UTF8) : null;
        };
        // 말맛 규칙(ChatVoice.cs)은 시세 반응 파일(chat_market)에 적혀 있다. 인증 반응도 같은 규칙으로 입힌다.
        ChatProofAssets.Reader = reader("chat_proof"); ChatDawnAssets.Reader = reader("chat_dawn"); ChatReactionAssets.Reader = reader("chat_market");
        try
        {
            ChatProofAssets.Reload(); ChatDawnAssets.Reload(); ChatReactionAssets.Reload();
            foreach (string problem in ChatProofAssets.Problems) { failures.Add("인증 대사 파일: " + problem); }
            foreach (string problem in ChatDawnAssets.Problems) { failures.Add("새벽 관찰실 대사 파일: " + problem); }
            foreach (string problem in ChatReactionAssets.Problems) { failures.Add("시세 반응 대사 파일(말맛 규칙): " + problem); }
            ProofSection(report, failures);
            ProofVoiceSection(report, failures);
            DawnSection(report, failures);
        }
        finally
        {
            // 점검이 끝나면 게임이 다시 Resources에서 읽게 되돌린다.
            ChatProofAssets.Reset(); ChatDawnAssets.Reset(); ChatReactionAssets.Reset();
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

    private static void AuditSlots(string where, string text, HashSet<string> known, List<string> failures)
    {
        for (int open = text.IndexOf('{'); open >= 0; open = text.IndexOf('{', open + 1))
        {
            int close = text.IndexOf('}', open + 1);
            string slot = close < 0 ? text.Substring(open) : text.Substring(open + 1, close - open - 1);
            if (!known.Contains(slot)) { failures.Add(where + ": 모르는 빈칸 {" + slot + "} — " + text); }
        }
    }

    // ── 1) 인증 카드 ──
    private static void ProofSection(StringBuilder report, List<string> failures)
    {
        report.AppendLine("■ 인증 카드 반응 (금액 × 수익률)");
        report.AppendLine("  금액 단: 푼돈 < " + InvestorSociety.Money(ChatProofMath.PocketMax) + " · 밥값 < " + InvestorSociety.Money(ChatProofMath.LunchMax) + " · 괜찮은 하루 < " + InvestorSociety.Money(ChatProofMath.GoodDayMax) +
            " · 월급 < " + InvestorSociety.Money(ChatProofMath.SalaryMax) + " · 차 한 대 < " + InvestorSociety.Money(ChatProofMath.CarMax) + " · 그 위는 인생이 바뀌는 돈");
        report.AppendLine("  수익률 단: 얇다 < " + ChatProofMath.ThinMax + "% · 쏠쏠 < " + ChatProofMath.SolidMax + "% · 크다 < " + ChatProofMath.BigMax + "% · 그 위는 엄청나다");
        report.AppendLine("  보는 사람: 그 돈이 자기 재산의 " + (ChatProofMath.UnimpressedShare * 100m).ToString("0.##") + "%도 안 되면 한 단 낮게, 재산 전체 이상이면 한 단 높게 읽는다(푼돈은 올리지 않는다).");
        report.AppendLine();
        report.AppendLine("  기분 표(가로: 수익률 얇다 / 쏠쏠 / 크다 / 엄청나다)");
        foreach (bool gain in new[] { true, false })
        {
            foreach (ChatProofAmountTier tier in Enum.GetValues(typeof(ChatProofAmountTier)))
            {
                var row = new StringBuilder("    " + (gain ? "수익 " : "손실 ") + tier.ToString().PadRight(8));
                decimal amount = tier == ChatProofAmountTier.Pocket ? 5_000m : tier == ChatProofAmountTier.Lunch ? 500_000m : tier == ChatProofAmountTier.GoodDay ? 5_000_000m :
                    tier == ChatProofAmountTier.Salary ? 50_000_000m : tier == ChatProofAmountTier.Car ? 500_000_000m : 5_000_000_000m;
                foreach (decimal percent in new[] { 1m, 8m, 30m, 80m })
                { row.Append(ChatProofMath.Read(false, gain ? amount : -amount, gain ? percent : -percent, 0m, 0m).mood.ToString().PadRight(12)); }
                report.AppendLine(row.ToString());
            }
        }
        report.AppendLine();

        // 대사 묶음: 기분마다 여덟 줄 이상, 모르는 빈칸 없음.
        report.AppendLine("  대사 묶음(기분별 줄 수: 기본 + 보유 전용 + 실현 전용 + 놀림)");
        foreach (string code in Languages)
        {
            if (!ChatProofAssets.Has(code)) { failures.Add(code + ": 인증 반응 대사가 없다(chat_proof." + code + ".json)"); continue; }
            int total = 0; var row = new StringBuilder("    [" + code + "]");
            foreach (ChatProofMood mood in Enum.GetValues(typeof(ChatProofMood)))
            {
                int basic = ChatProofAssets.Lines(code, mood.ToString()).Length, hold = ChatProofAssets.Lines(code, mood + ".hold").Length, sold = ChatProofAssets.Lines(code, mood + ".sold").Length, edge = ChatProofAssets.Lines(code, mood + ".edge").Length;
                total += basic + hold + sold + edge;
                row.Append(" " + mood + " " + basic + "+" + hold + "+" + sold + "+" + edge);
                if (basic < 8) { failures.Add(code + " " + mood + ": 대사가 " + basic + "줄뿐이다(8줄 이상이어야 한다)"); }
                int plain = 0;
                foreach (string suffix in new[] { "", ".hold", ".sold", ".edge" })
                {
                    foreach (string text in ChatProofAssets.Lines(code, mood + suffix))
                    {
                        AuditSlots(code + " proof." + mood + suffix, text, ProofSlots, failures);
                        if (suffix.Length == 0 && text.IndexOf('{') < 0) { plain++; }
                    }
                }
                // 대개는 숫자 없이 말한다.
                if (plain * 2 < basic) { failures.Add(code + " " + mood + ": 숫자 없는 문장이 절반이 안 된다(" + plain + "/" + basic + ")"); }
            }
            report.AppendLine(row.ToString());
            report.AppendLine("        모두 " + total + "줄");
        }
        report.AppendLine();

        foreach (var test in ProofCases())
        {
            var read = ChatProofMath.Read(test.holding, test.amount, test.percent, test.stake, test.viewer);
            report.AppendLine("  ──────────────────────────────────────────────");
            report.AppendLine("  ▷ " + test.name + " — " + test.note);
            report.AppendLine("    보는 사람의 재산 " + InvestorSociety.Money(test.viewer) + " · 금액 단 " + read.rawTier + (read.tier != read.rawTier ? " → " + read.tier + "(재산에 견줘)" : "") +
                " · 수익률 단 " + read.percentTier + (test.holding ? " · 들고 있는 크기 " + InvestorSociety.Money(read.stake) : "") + " · 기분 " + read.mood);
            if (read.mood != test.expected) { failures.Add(test.name + ": 기분이 " + read.mood + "다(" + test.expected + "여야 한다)"); }
            if (test.noAwe && ChatProofMath.Awed(read.mood)) { failures.Add(test.name + ": 놀라는 기분(" + read.mood + ")으로 읽혔다"); }
            if (test.noTease && ChatProofMath.TeasesSmall(read.mood)) { failures.Add(test.name + ": 금액이 작다고 놀리는 기분(" + read.mood + ")으로 읽혔다"); }
            foreach (string code in Languages)
            {
                if (!ChatProofAssets.Has(code)) { continue; }
                var slots = ChatProofText.Slots(code, read, StockNames[code]);
                report.AppendLine("    [" + code + "] 금액 " + slots["amount"] + " · 수익률 " + slots["pct"]);
                var random = new System.Random(Seed(test.name + code)); var used = new HashSet<string>(StringComparer.Ordinal);
                // 여러 번 뽑아서 엉뚱한 묶음의 말이 섞이지 않는지 본다(놀라는 말, 금액이 작다고 놀리는 말).
                for (int i = 0; i < 60; i++)
                {
                    string line = ChatProofAssets.Pick(code, read, i % 2 == 0, null, slots, random.NextDouble, i < 3 ? used.Contains : (Func<string, bool>)null, out string key);
                    if (line == null) { failures.Add(test.name + " " + code + ": 고를 대사가 없다"); break; }
                    foreach (ChatProofMood other in Enum.GetValues(typeof(ChatProofMood)))
                    {
                        if (other == read.mood || !(test.noAwe && ChatProofMath.Awed(other) || test.noTease && ChatProofMath.TeasesSmall(other))) { continue; }
                        foreach (string suffix in new[] { "", ".hold", ".sold", ".edge" })
                        { if (Array.IndexOf(ChatProofAssets.Lines(code, other + suffix), line) >= 0) { failures.Add(test.name + " " + code + ": " + other + " 묶음의 말이 나왔다 — " + line); } }
                    }
                    string said = ChatHypeAssets.Fill(line, slots);
                    if (said.IndexOf('{') >= 0 || said.IndexOf("%%", StringComparison.Ordinal) >= 0 || said.IndexOf("퍼%", StringComparison.Ordinal) >= 0) { failures.Add(test.name + " " + code + ": 빈칸이 남았거나 %가 겹쳤다 — " + said); }
                    if (i < 3 && used.Add(line)) { report.AppendLine("        " + said + "    (" + key + ")"); }
                }
                if (used.Count < 3) { failures.Add(test.name + " " + code + ": 서로 다른 대사가 세 줄이 안 된다"); }
            }
        }
        report.AppendLine();
    }

    // ── 1-2) 인증 반응의 말맛 ──
    /// <summary>
    /// 인증 반응에 말맛(ChatVoice.cs)을 입힌 뒤의 모습. 게임이 쓰는 것과 같은 길(InvestorSociety.VoicedProof)을 거친다. 세기는 금액 단에서 온다.
    ///   1) 언어마다 40줄을 여러 카드·세기에 걸쳐 적는다(사람이 읽고 말투를 판단하라고). 원문과 달라진 줄에는 원문을 같이 적는다.
    ///   2) 카드마다 여러 번 뽑아 따진다: 빈 줄이 없는가, 빈칸이 남지 않았는가, 말끝이 겹치지 않았는가("ㄷㄷ ㄷㄷ", "lol lol"),
    ///      금액이 작은 카드에 긴 ㅋ·ㄷ·ｗ 줄이나 길게 늘인 글자가 붙지 않았는가. 위로하는 말과 새벽 관찰실의 말에는 말머리가 붙지 않는가.
    ///   3) 카드에 다는 맞장구를 세기별로 적는다.
    /// </summary>
    private static void ProofVoiceSection(StringBuilder report, List<string> failures)
    {
        report.AppendLine("════════════════════════════════════════════════════════════");
        report.AppendLine("■ 인증 반응의 말맛(런타임 변주) — 세기는 금액 단이 정한다: 푼돈·밥값 0, 괜찮은 하루 1, 월급 2, 차 한 대부터 3 (판돈에 놀라는 말은 2부터)");
        report.AppendLine("  [ ] 안은 그 줄의 세기와 기분, 화살표 뒤는 파일에 적힌 원문이다.");
        report.AppendLine();
        var cases = ProofCases();
        foreach (string code in Languages)
        {
            if (!ChatProofAssets.Has(code)) { continue; }
            var engine = ChatReactionAssets.Get(code); var voice = engine?.Voice;
            if (voice == null) { failures.Add(code + ": 말맛 규칙이 없다(chat_market." + code + ".json의 voice)"); continue; }
            var tails = new List<string>(engine.Tails);

            // 1) 견본 40줄.
            report.AppendLine("  ── [" + code + "] 견본 40줄 ──");
            var random = new System.Random(Seed("proofVoice" + code)); var shown = new HashSet<string>(StringComparer.Ordinal); int printed = 0;
            for (int round = 0; printed < 40 && round < 10; round++)
            {
                foreach (var test in cases)
                {
                    if (printed >= 40) { break; }
                    var read = ChatProofMath.Read(test.holding, test.amount, test.percent, test.stake, test.viewer);
                    var slots = ChatProofText.Slots(code, read, StockNames[code]);
                    string line = ChatProofAssets.Pick(code, read, false, null, slots, random.NextDouble, shown.Contains, out string key);
                    if (line == null || !shown.Add(line)) { continue; }
                    string said = ChatHypeAssets.Fill(InvestorSociety.VoicedProof(line, code, read, false, false, unchecked((uint)random.Next()), random.NextDouble), slots), plain = ChatHypeAssets.Fill(line, slots);
                    report.AppendLine("      [" + ChatVoice.ProofLevel(read) + " " + read.mood + "] " + said + "    (" + key + (said == plain ? "" : " ← " + plain) + ")");
                    printed++;
                }
            }
            if (printed < 40) { failures.Add(code + ": 인증 반응의 말맛 견본이 " + printed + "줄뿐이다(40줄이어야 한다)"); }

            // 2) 카드마다 여러 번 뽑아서 따진다. 세 가지 길: 평소, 위로하는 말(gentle), 새벽 관찰실(measured).
            int looked = 0, changed = 0, problems = 0;
            foreach (var test in cases)
            {
                var read = ChatProofMath.Read(test.holding, test.amount, test.percent, test.stake, test.viewer);
                var slots = ChatProofText.Slots(code, read, StockNames[code]);
                var pick = new System.Random(Seed("proofSweep" + test.name + code));
                for (int i = 0; i < 90; i++)
                {
                    bool gentle = i % 3 == 1, measured = i % 3 == 2;
                    string line = ChatProofAssets.Pick(code, read, gentle, null, slots, pick.NextDouble, null, out _);
                    if (line == null) { continue; }
                    int seed = Seed(code + "|" + test.name + "|" + i); uint habit = unchecked((uint)(seed * 2654435761u + i * 40503u));
                    string dressed = InvestorSociety.VoicedProof(line, code, read, gentle, measured, habit, new System.Random(seed).NextDouble);
                    string said = ChatHypeAssets.Fill(dressed, slots), plain = ChatHypeAssets.Fill(line, slots);
                    looked++; if (said != plain) { changed++; }
                    string problem = ChatVoice.Audit(said, voice, measured ? ChatVoice.Mild : ChatVoice.ProofLevel(read), tails);
                    // 위로하는 말, 새벽 관찰실의 말, 남의 손실을 두고 하는 말은 앞에 아무것도 붙지 않고(말머리·끼우는 말 없이, 줄 길이만 다듬는다) 대문자로 소리치지도 않는다.
                    if (problem == null && (gentle || measured || !read.gain) && said != plain && said == said.ToUpperInvariant() && plain != plain.ToUpperInvariant() && string.Equals(said, plain, StringComparison.OrdinalIgnoreCase))
                    { problem = "소리치면 안 되는 말을 대문자로 쳤다"; }
                    if (problem == null && (gentle || measured || !read.gain) &&said.Length > 0 && plain.Length > 0 && char.ToLowerInvariant(said[0]) != char.ToLowerInvariant(plain[0])) { problem = (gentle ? "위로하는 말" : measured ? "새벽 관찰실의 말" : "손실 카드에 다는 말") + "에 말머리가 붙었다"; }
                    if (problem != null && ++problems <= 12) { failures.Add(code + " " + test.name + " 말맛: " + problem + " — " + said + " (원문: " + line + ")"); }
                }
            }
            if (problems > 12) { failures.Add(code + ": 인증 반응의 말맛 문제가 " + (problems - 12) + "건 더 있다"); }
            report.AppendLine("  ── [" + code + "] 검사: " + looked + "줄(카드 " + cases.Count + "장 × 90번: 평소·위로·새벽 관찰실) · 원문과 달라진 줄 " + changed + "(" + (looked == 0 ? 0 : changed * 100 / looked) + "%) · 문제 " + problems + "건 ──");

            // 3) 카드에 다는 맞장구: 큰돈에는 놀라고(awe), 푼돈에는 웃고(laugh), 손실에는 같이 아파한다(sad).
            report.AppendLine("  ── [" + code + "] 카드에 다는 맞장구(세기 0 → 3) ──");
            var backRandom = new System.Random(Seed("proofBack" + code));
            foreach (string mood in new[] { "awe", "laugh", "sad", "agree" })
            {
                var row = new StringBuilder("      " + mood.PadRight(6));
                for (int level = 0; level <= 3; level++)
                {
                    string back = ChatVoice.Back(voice, mood, level, backRandom.NextDouble, null);
                    string problem = ChatVoice.Audit(back, voice, level, tails);
                    if (problem != null) { failures.Add(code + " 맞장구 " + mood + ": " + problem + " — " + back); }
                    row.Append(level == 0 ? "" : "  |  ").Append(back);
                }
                report.AppendLine(row.ToString());
            }
            report.AppendLine();
        }
    }

    // ── 2) 새벽 관찰실 ──
    private static void DawnSection(StringBuilder report, List<string> failures)
    {
        report.AppendLine("════════════════════════════════════════════════════════════");
        report.AppendLine("■ 새벽 관찰실 대사");
        report.AppendLine("  대사 묶음(줄 수 · 값을 대는 문장 수)");
        foreach (string code in Languages)
        {
            if (!ChatDawnAssets.Has(code)) { failures.Add(code + ": 새벽 관찰실 대사가 없다(chat_dawn." + code + ".json)"); continue; }
            int total = 0, groundedTotal = 0; var row = new StringBuilder("    [" + code + "]");
            foreach (string key in ChatDawnAssets.RequiredGroups())
            {
                string[] lines = ChatDawnAssets.Lines(code, key); int grounded = 0;
                foreach (string text in lines)
                {
                    AuditSlots(code + " dawn." + key, text, DawnSlots, failures);
                    if (ChatDawnTalk.Grounded(text)) { grounded++; }
                    foreach (string mark in Noise) { if (text.IndexOf(mark, StringComparison.OrdinalIgnoreCase) >= 0) { failures.Add(code + " dawn." + key + ": 호들갑 표시(" + mark + ")가 들어 있다 — " + text); } }
                    if (text.Trim().Length < 6) { failures.Add(code + " dawn." + key + ": 한두 마디짜리 말이다 — " + text); }
                }
                total += lines.Length; groundedTotal += grounded;
                row.Append(" " + key + " " + lines.Length + "·" + grounded);
                if (lines.Length < 5) { failures.Add(code + " dawn." + key + ": 대사가 " + lines.Length + "줄뿐이다(5줄 이상이어야 한다)"); }
                // 관찰·답·의심·반박·자기 매매는 값을 대고 말해야 한다(절반 이상). 값이 없을 때 쓸 문장도 하나는 있어야 한다.
                bool mustGround = key.StartsWith("read.", StringComparison.Ordinal) || key.StartsWith("answer.", StringComparison.Ordinal) || key.StartsWith("doubt.", StringComparison.Ordinal) || key.StartsWith("rebut.", StringComparison.Ordinal) || key.StartsWith("fill.", StringComparison.Ordinal);
                if (mustGround && grounded * 2 < lines.Length) { failures.Add(code + " dawn." + key + ": 값을 대는 문장이 절반이 안 된다(" + grounded + "/" + lines.Length + ")"); }
                bool fallback = false;
                foreach (string text in lines) { if (text.IndexOf('{') < 0 || text.Replace("{price}", "").IndexOf('{') < 0) { fallback = true; break; } }
                if (!fallback) { failures.Add(code + " dawn." + key + ": 고가·저가·호가 값이 없을 때 쓸 문장이 없다"); }
            }
            report.AppendLine(row.ToString());
            report.AppendLine("        모두 " + total + "줄 · 값을 대는 문장 " + groundedTotal + "줄(" + (total == 0 ? 0 : groundedTotal * 100 / total) + "%)");
        }
        report.AppendLine();

        var scenes = DawnScenes();
        foreach (string code in Languages)
        {
            if (!ChatDawnAssets.Has(code)) { continue; }
            report.AppendLine("  ── [" + code + "] 이 방에 뜰 대화 ──");
            // 같은 문장은 견본 전체에서 한 번만 쓴다(게임에서도 방금 나온 문장은 피한다).
            var seen = new HashSet<string>(StringComparer.Ordinal); var used = new HashSet<string>(StringComparer.Ordinal); int count = 0, withNumber = 0;
            foreach (var scene in scenes)
            {
                var sim = ChatSituationSim.Run(10000m, scene.path); var read = sim.final;
                var bucket = ChatDawnTalk.Bucket(read.kind);
                if (bucket != scene.expected) { failures.Add("장면 " + scene.name + ": " + read.kind + "(" + bucket + ")로 읽혔다(" + scene.expected + "여야 한다)"); }
                report.AppendLine("    〈" + scene.name + "〉 " + scene.note + " · " + read.kind + " · 현재 " + InvestorSociety.Money(read.price) + " · 전일 대비 " + ChatSituationMath.SignedPercent(read.dayPct));
                var random = new System.Random(Seed(scene.name + code));
                foreach (var step in scene.steps)
                {
                    var stance = step.stance == 0 ? ChatStance.Gain : step.stance == 1 ? ChatStance.Loss : ChatStance.Watch; bool held = step.stance != 2 && !step.sold;
                    string group = ChatDawnTalk.Group(step.act, step.parent, step.self, bucket, stance, step.view, step.parentView, step.bought, step.sold, held);
                    if (group == null) { failures.Add("장면 " + scene.name + " " + step.who + ": 묶음이 정해지지 않았다(" + step.act + ")"); continue; }
                    var slots = new Dictionary<string, string>(StringComparer.Ordinal) { ["stockName"] = StockNames[code], ["price"] = InvestorSociety.Money(read.price) };
                    ChatDawnTalk.FillRead(read, slots);
                    if (held)
                    {
                        decimal cost = step.stance == 0 ? scene.gainCost : scene.lossCost;
                        slots["avg"] = InvestorSociety.Money(cost); slots["myPct"] = ChatSituationMath.SignedPercent((read.price / cost - 1m) * 100m);
                    }
                    // 호가창의 큰 잔량은 지어낸 값이다(현재가 조금 아래의 매수 잔량, 조금 위의 매도 잔량).
                    slots["bidWall"] = InvestorSociety.Money(PriceTickPolicy.RoundToTick(read.price * .985m)); slots["askWall"] = InvestorSociety.Money(PriceTickPolicy.RoundToTick(read.price * 1.02m));
                    ChatDawnTalk.NarrowForPlan(group, read, slots);
                    string line = ChatDawnTalk.Pick(code, group, slots, random.NextDouble, index => used.Contains(group + "." + index), out int number);
                    if (line == null) { failures.Add("장면 " + scene.name + " " + code + " " + group + ": 고를 대사가 없다"); continue; }
                    used.Add(group + "." + number);
                    // 게임에서도 혼자 꺼내는 말 앞에는 종목 이름을 붙인다. 답글에는 붙이지 않는다.
                    if (!step.parent.HasValue && line.IndexOf("{stockName}", StringComparison.Ordinal) < 0) { line = "{stockName} " + line; }
                    string said = ChatHypeAssets.Fill(line, slots);
                    if (said.IndexOf('{') >= 0 || said.IndexOf("%%", StringComparison.Ordinal) >= 0) { failures.Add("장면 " + scene.name + " " + code + ": 빈칸이 남았거나 %가 겹쳤다 — " + said); }
                    if (!seen.Add(said)) { failures.Add("장면 " + scene.name + " " + code + ": 같은 말이 두 번 나왔다 — " + said); }
                    count++;
                    foreach (char c in said) { if (c >= '0' && c <= '9') { withNumber++; break; } }
                    report.AppendLine("      " + step.who + (step.parent.HasValue ? " ↳ " : " : ") + said + "    (" + group + "." + number + ")");
                }
            }
            report.AppendLine("    모두 " + count + "줄 · 숫자를 댄 말 " + withNumber + "줄");
            if (count < 30) { failures.Add(code + ": 새벽 관찰실 견본이 " + count + "줄뿐이다(30줄이어야 한다)"); }
            if (withNumber * 2 < count) { failures.Add(code + ": 숫자를 댄 말이 절반이 안 된다(" + withNumber + "/" + count + ")"); }
            report.AppendLine();
        }
    }
}
