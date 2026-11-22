using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

// 프레임이 갑자기 떨어지는 순간을 잡는 도구. 편집기의 프로파일러를 켜 두고, 한 프레임이 문턱(기본 100ms)을 넘으면
// 그 프레임에서 시간을 가장 많이 쓴 길(위에서 아래로 가장 무거운 자식을 따라 내려간 것)과 자기 시간이 큰 함수들을 적는다.
// PerfProbe가 평균을 본다면 이것은 튀는 프레임만 본다. 게임 코드는 건드리지 않는다. 재는 동안 프레임 제한을 푼다.
public static class FrameSpikes
{
    /// <summary>이보다 오래 걸린 프레임을 적는다(ms).</summary>
    public static float Threshold = 100f;
    private const int Detailed = 40;
    private static readonly StringBuilder log = new();
    private static readonly List<float> times = new(32768), gameTimes = new(32768);
    /// <summary>가장 느린 프레임들의 풀이(게임 쪽 시간 순). 처음 몇 개가 아니라 가장 나쁜 것들을 남긴다.</summary>
    private static readonly List<(float ms, string text)> worst = new();
    /// <summary>게임 코드 표식마다: 2ms 넘게 쓴 프레임 수, 가장 오래 쓴 프레임, 모두 더한 시간.</summary>
    private static readonly Dictionary<string, (int heavy, float max, double total)> costs = new();
    private static readonly Stack<int> walk = new();
    private static readonly Dictionary<string, (int count, double ms)> culprits = new();
    private static readonly List<int> children = new();
    private static int next, spikes, savedRate, savedSync;
    private static double began;
    public static bool Running { get; private set; }
    public static int Spikes => spikes;

    public static void Begin()
    {
        log.Clear(); times.Clear(); gameTimes.Clear(); worst.Clear(); costs.Clear(); culprits.Clear(); spikes = 0; next = -1;
        ProfilerDriver.ClearAllFrames(); ProfilerDriver.profileEditor = false; ProfilerDriver.deepProfiling = false;
        Profiler.enabled = true; ProfilerDriver.enabled = true;
        savedRate = Application.targetFrameRate; savedSync = QualitySettings.vSyncCount;
        Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        began = UnityEditor.EditorApplication.timeSinceStartup; Running = true;
    }

    /// <summary>새로 모인 프레임을 읽는다. 자주 부른다(프로파일러는 최근 프레임만 들고 있다). stamp는 그 순간의 게임 시각 같은 꼬리표.</summary>
    public static void Poll(string stamp)
    {
        if (!Running) { return; }
        int last = ProfilerDriver.lastFrameIndex, first = ProfilerDriver.firstFrameIndex;
        if (last < 0) { return; }
        if (next < first) { next = first; }
        // 마지막 프레임은 아직 쓰는 중일 수 있어 남겨 둔다.
        for (; next < last; next++)
        {
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(next, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
            {
                if (view == null || !view.valid) { continue; }
                // 편집기에서 돌리면 편집기 자신이 쓰는 시간(EditorLoop)이 프레임마다 섞인다. 게임이 쓴 시간(PlayerLoop)만 따로 잰다.
                float ms = view.frameTimeMs; times.Add(ms);
                float game = Game(view); gameTimes.Add(game);
                if (game < Threshold) { continue; }
                spikes++;
                int before = log.Length;
                Describe(view, game, stamp, true);
                string text = log.ToString(before, log.Length - before); log.Length = before;
                worst.Add((game, text));
                if (worst.Count > Detailed * 2) { worst.Sort((a, b) => b.ms.CompareTo(a.ms)); worst.RemoveRange(Detailed, worst.Count - Detailed); }
            }
        }
    }

    /// <summary>이 프레임에서 게임이 쓴 시간(ms)을 돌려주고, 게임 코드 표식들의 시간을 모아 둔다.</summary>
    private static float Game(HierarchyFrameDataView view)
    {
        float game = 0f; int loop = -1;
        children.Clear(); view.GetItemChildren(view.GetRootItemID(), children);
        foreach (int child in children) { if (view.GetItemName(child) == "PlayerLoop") { loop = child; game = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime); } }
        if (loop < 0) { return 0f; }
        walk.Clear(); walk.Push(loop);
        while (walk.Count > 0)
        {
            int current = walk.Pop(); children.Clear(); view.GetItemChildren(current, children);
            foreach (int child in children)
            {
                float total = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime);
                if (total < .3f) { continue; }
                string name = view.GetItemName(child);
                if (name.StartsWith("DonDon.", StringComparison.Ordinal) || name.StartsWith("Assembly-CSharp", StringComparison.Ordinal) || name.StartsWith("Canvas", StringComparison.Ordinal) || name.StartsWith("TMP", StringComparison.Ordinal) || name.StartsWith("GC", StringComparison.Ordinal) || name.StartsWith("Font", StringComparison.Ordinal))
                {
                    costs.TryGetValue(name, out var seen);
                    costs[name] = (seen.heavy + (total >= 2f ? 1 : 0), Mathf.Max(seen.max, total), seen.total + total);
                }
                walk.Push(child);
            }
        }
        return game;
    }

    private static void Describe(HierarchyFrameDataView view, float ms, string stamp, bool detailed)
    {
        // 가장 무거운 길: 뿌리에서 시작해 전체 시간이 가장 큰 자식을 따라 내려간다. 프레임의 절반 넘게 차지하는 가장 깊은 곳이 범인이다.
        var path = new List<(string name, float total, float self, float gc)>();
        int item = view.GetRootItemID(); string culprit = "(unknown)";
        for (int depth = 0; depth < 24; depth++)
        {
            children.Clear(); view.GetItemChildren(item, children);
            int heaviest = -1; float most = 0f;
            foreach (int child in children) { float total = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime); if (total > most) { most = total; heaviest = child; } }
            if (heaviest < 0 || most < ms * .04f) { break; }
            string name = view.GetItemName(heaviest);
            path.Add((name, most, view.GetItemColumnDataAsFloat(heaviest, HierarchyFrameDataView.columnSelfTime), view.GetItemColumnDataAsFloat(heaviest, HierarchyFrameDataView.columnGcMemory)));
            if (most >= ms * .5f) { culprit = name; }
            item = heaviest;
        }
        // 엔진의 겉 이름(PlayerLoop, Update.ScriptRunBehaviourUpdate …)에서 멈췄으면 그 아래 첫 줄을 범인으로 적는다.
        culprits[culprit] = culprits.TryGetValue(culprit, out var seen) ? (seen.count + 1, seen.ms + ms) : (1, ms);
        if (!detailed) { return; }
        log.AppendLine($"SPIKE {ms:0} ms ({1000f / ms:0.0} fps)  at {UnityEditor.EditorApplication.timeSinceStartup - began:0.0}s  {stamp}");
        for (int i = 0; i < path.Count; i++)
        { log.AppendLine($"  {new string(' ', Math.Min(i, 16))}{path[i].total,7:0.0} ms  self {path[i].self,6:0.0}  gc {path[i].gc / 1024f,7:0} KB  {path[i].name}"); }
        // 자기 시간이 큰 함수들.
        var own = new List<(string name, float self, float calls)>(); var stack = new Stack<int>(); stack.Push(view.GetRootItemID());
        while (stack.Count > 0)
        {
            int current = stack.Pop(); children.Clear(); view.GetItemChildren(current, children);
            foreach (int child in children)
            {
                float self = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime);
                if (self >= ms * .03f) { own.Add((view.GetItemName(child), self, view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnCalls))); }
                if (view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime) >= ms * .03f) { stack.Push(child); }
            }
        }
        foreach (var entry in own.OrderByDescending(e => e.self).Take(8)) { log.AppendLine($"    self {entry.self,7:0.0} ms  calls {entry.calls,6:0}  {entry.name}"); }
    }

    public static string End(string label)
    {
        Poll("end");
        Profiler.enabled = false; ProfilerDriver.enabled = false; Running = false;
        Application.targetFrameRate = savedRate; QualitySettings.vSyncCount = savedSync;
        var text = new StringBuilder();
        text.AppendLine("== frame spikes: " + label + " ==");
        if (times.Count == 0) { text.AppendLine("no frames captured"); return text.ToString(); }
        var sorted = new List<float>(times); sorted.Sort();
        float At(float share) => sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * share))];
        text.AppendLine($"frames {times.Count} over {UnityEditor.EditorApplication.timeSinceStartup - began:0}s · ms: average {times.Average():0.0} (fps {1000f / times.Average():0}) median {At(.5f):0.0} p95 {At(.95f):0.0} p99 {At(.99f):0.0} worst {sorted[sorted.Count - 1]:0}");
        text.AppendLine($"frames over 100 ms: {times.Count(t => t >= 100f)}, over 250 ms: {times.Count(t => t >= 250f)}, over 500 ms (2 fps): {times.Count(t => t >= 500f)}");
        if (gameTimes.Count > 0)
        {
            var game = new List<float>(gameTimes); game.Sort();
            float G(float share) => game[Mathf.Min(game.Count - 1, (int)(game.Count * share))];
            text.AppendLine($"game side only (PlayerLoop, without the editor's own time) · ms: average {gameTimes.Average():0.0} median {G(.5f):0.0} p95 {G(.95f):0.0} p99 {G(.99f):0.0} worst {game[game.Count - 1]:0}");
            text.AppendLine($"game frames over 16.7 ms: {gameTimes.Count(t => t >= 16.7f)}, over 33 ms: {gameTimes.Count(t => t >= 33f)}, over 50 ms: {gameTimes.Count(t => t >= 50f)}, over 100 ms: {gameTimes.Count(t => t >= 100f)}");
            text.AppendLine("game code cost over the whole run (frames using 2 ms or more · worst frame ms · total ms):");
            foreach (var pair in costs.OrderByDescending(p => p.Value.max).Take(45)) { text.AppendLine($"  {pair.Value.heavy,6}  {pair.Value.max,7:0.0}  {pair.Value.total,9:0}  {pair.Key}"); }
        }
        text.AppendLine("what the slow frames were doing (deepest sample holding over half the frame · count · total ms):");
        foreach (var pair in culprits.OrderByDescending(p => p.Value.ms)) { text.AppendLine($"  {pair.Value.count,4}  {pair.Value.ms,8:0} ms  {pair.Key}"); }
        text.AppendLine();
        worst.Sort((a, b) => b.ms.CompareTo(a.ms));
        foreach (var entry in worst.Take(Detailed)) { text.Append(entry.text); }
        return text.ToString();
    }
}
