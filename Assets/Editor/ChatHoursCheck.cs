using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 시간대 잡담(Chat/chat_hours.*)의 검사. 게임을 돌리지 않고 자료와 고르는 규칙만 본다.
///   - 언어마다 파일이 읽히는가, 화제마다 꺼내는 말·받는 말이 충분한가, 빈칸({…})이나 @가 섞이지 않았는가, 같은 문장이 겹치지 않는가
///   - 0~23시의 모든 시각에 꺼낼 화제가 둘 이상 있는가
///   - 시각마다 어떤 화제가 얼마나 나오는지, 그리고 대화 한 토막의 본보기
/// 결과는 Validation/ChatSituation/hours-report.txt. 마지막 줄이 RESULT: PASS / FAIL.
/// </summary>
public static class ChatHoursCheck
{
    [MenuItem("DonDon/검사/시간대 잡담")]
    public static void Run() { Debug.Log(Check() ? "시간대 잡담 검사: 통과" : "시간대 잡담 검사: 실패 (Validation/ChatSituation/hours-report.txt)"); }
    public static void RunBatch() { EditorApplication.Exit(Check() ? 0 : 1); }

    private static bool Check()
    {
        var report = new StringBuilder();
        int failures = 0;
        var random = new System.Random(4127);
        foreach (string code in new[] { "ko", "en", "ja" })
        {
            var topics = ChatHoursAssets.For(code);
            int lines = 0;
            foreach (var topic in topics) { lines += topic.open.Length + topic.reply.Length + (topic.back?.Length ?? 0); }
            report.AppendLine("══ [" + code + "] 화제 " + topics.Length + "개 · 문장 " + lines + "줄 ══");
            if (topics.Length == 0) { report.AppendLine("  - FAIL 파일이 없거나 읽히지 않는다"); failures++; continue; }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var topic in topics)
            {
                if (!keys.Add(topic.key)) { report.AppendLine("  - FAIL 같은 열쇠가 두 번: " + topic.key); failures++; }
                if (topic.fromHour < 0 || topic.fromHour > 23 || topic.toHour < 0 || topic.toHour > 24 || topic.fromHour == topic.toHour)
                { report.AppendLine("  - FAIL 시각이 틀렸다: " + topic.key + " " + topic.fromHour + "~" + topic.toHour); failures++; }
                if (topic.open.Length < 5 || topic.reply.Length < 8) { report.AppendLine("  - FAIL 문장이 모자란다: " + topic.key + " open " + topic.open.Length + " reply " + topic.reply.Length); failures++; }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var pool in new[] { topic.open, topic.reply, topic.back ?? Array.Empty<string>() })
                {
                    foreach (string line in pool)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.IndexOf('{') >= 0 || line.IndexOf('}') >= 0 || line.IndexOf('@') >= 0 || line.Length > 70)
                        { report.AppendLine("  - FAIL 쓸 수 없는 문장(" + topic.key + "): " + line); failures++; }
                        if (!seen.Add(line)) { report.AppendLine("  - FAIL 같은 문장이 두 번(" + topic.key + "): " + line); failures++; }
                    }
                }
            }
            for (int hour = 0; hour < 24; hour++)
            {
                var names = new List<string>(); double total = 0;
                foreach (var topic in topics) { if (ChatHoursAssets.InHours(topic, hour)) { names.Add(topic.key); total += topic.weight; } }
                var shares = new List<string>();
                foreach (var topic in topics) { if (ChatHoursAssets.InHours(topic, hour)) { shares.Add(topic.key + " " + Math.Round(topic.weight / total * 100) + "%"); } }
                report.AppendLine("  " + hour.ToString("00") + "시  " + string.Join(" · ", shares));
                if (names.Count < 2) { report.AppendLine("  - FAIL " + hour + "시에 꺼낼 화제가 " + names.Count + "개뿐이다"); failures++; }
            }
            report.AppendLine("  ── 본보기(세 시간마다 한 토막) ──");
            for (int hour = 0; hour < 24; hour += 3)
            {
                var pool = new List<AfterHoursTopic>();
                foreach (var topic in topics) { if (ChatHoursAssets.InHours(topic, hour)) { pool.Add(topic); } }
                if (pool.Count == 0) { continue; }
                var picked = pool[random.Next(pool.Count)];
                report.AppendLine("  " + hour.ToString("00") + "시 " + picked.key);
                report.AppendLine("      A: " + picked.open[random.Next(picked.open.Length)]);
                int replies = 1 + random.Next(3); var used = new HashSet<int>();
                for (int i = 0; i < replies; i++)
                {
                    int at = random.Next(picked.reply.Length); if (!used.Add(at)) { continue; }
                    report.AppendLine("      " + (char)('B' + i) + ": @A " + picked.reply[at]);
                }
                if (picked.back != null && picked.back.Length > 0) { report.AppendLine("      A: " + picked.back[random.Next(picked.back.Length)]); }
            }
            report.AppendLine();
        }
        report.AppendLine("RESULT: " + (failures == 0 ? "PASS" : "FAIL (" + failures + ")"));
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Validation", "ChatSituation");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "hours-report.txt"), report.ToString(), new UTF8Encoding(false));
        return failures == 0;
    }
}
