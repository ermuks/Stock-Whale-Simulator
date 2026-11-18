using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

// 성능과 메모리를 재는 도구. 편집기의 프로파일러가 모은 프레임 자료를 읽어서,
// 어느 함수가 시간을 많이 쓰는지(자기 시간 기준), 프레임마다 얼마나 쓰레기(GC)를 만드는지, 물체 수가 계속 늘어나는지(누수)를 글로 남긴다.
// 촬영 도구(ScreenTourCapture)의 perf 단계가 부른다. 게임 코드는 건드리지 않는다.
public static class PerfProbe
{
    private static readonly Type[] Counted = { typeof(GameObject), typeof(Mesh), typeof(Material), typeof(Texture2D), typeof(RenderTexture), typeof(Sprite), typeof(MonoBehaviour), typeof(AudioClip), typeof(Shader) };
    private static Dictionary<string, int> before;
    private static long monoBefore, totalBefore;
    private static double startedAt;
    private static int firstFrame;

    /// <summary>재기 시작: 프로파일러를 켜고 지금의 물체 수와 메모리를 적어 둔다.</summary>
    public static void Begin()
    {
        GC.Collect();
        before = Count(); monoBefore = Profiler.GetMonoUsedSizeLong(); totalBefore = Profiler.GetTotalAllocatedMemoryLong();
        ProfilerDriver.ClearAllFrames(); ProfilerDriver.profileEditor = false; ProfilerDriver.deepProfiling = false;
        Profiler.enabled = true; ProfilerDriver.enabled = true;
        startedAt = UnityEditor.EditorApplication.timeSinceStartup; firstFrame = -1;
        // 검증 모드는 초당 20프레임으로 묶여 있다. 재는 동안에는 풀어서 실제 프레임 시간을 본다.
        savedRate = Application.targetFrameRate; savedSync = QualitySettings.vSyncCount;
        Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        censusBefore = Census();
    }
    private static int savedRate, savedSync;
    private static Dictionary<string, long> censusBefore;

    /// <summary>
    /// 게임 코드(Assembly-CSharp)의 물체들을 따라가며 목록·사전·큐 같은 모음마다 들어 있는 개수를 센다. 이름은 "클래스.필드"이고 같은 이름은 합친다.
    /// 두 번 세어 비교하면 무엇이 계속 쌓이는지(누수) 보인다.
    /// </summary>
    private static Dictionary<string, long> Census()
    {
        var result = new Dictionary<string, long>(); var seen = new HashSet<object>(ReferenceComparer.Instance); var queue = new Queue<object>();
        var game = typeof(MarketDayController).Assembly;
        foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        { if (behaviour != null && behaviour.GetType().Assembly == game && seen.Add(behaviour)) { queue.Enqueue(behaviour); } }
        foreach (var type in game.GetTypes())
        {
            if (type.IsGenericTypeDefinition) { continue; }
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (field.FieldType.IsPrimitive || field.FieldType == typeof(string) || field.IsLiteral) { continue; }
                object value; try { value = field.GetValue(null); } catch { continue; }
                Visit(value, type.Name + "." + field.Name + " (static)", result, seen, queue, game);
            }
        }
        int guard = 0;
        while (queue.Count > 0 && guard++ < 3000000)
        {
            object item = queue.Dequeue();
            for (var type = item.GetType(); type != null && type != typeof(object) && type != typeof(MonoBehaviour) && type != typeof(Component) && type != typeof(UnityEngine.Object); type = type.BaseType)
            {
                if (type.Assembly != game) { continue; }
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string) || field.FieldType == typeof(decimal)) { continue; }
                    object value; try { value = field.GetValue(item); } catch { continue; }
                    Visit(value, type.Name + "." + field.Name, result, seen, queue, game);
                }
            }
        }
        return result;
    }
    private static void Visit(object value, string key, Dictionary<string, long> result, HashSet<object> seen, Queue<object> queue, System.Reflection.Assembly game)
    {
        if (value == null || value is UnityEngine.Object unity && unity == null) { return; }
        var type = value.GetType();
        if (type.IsValueType && !(value is System.Collections.ICollection)) { return; }
        if (value is string) { return; }
        if (value is System.Collections.ICollection collection)
        {
            if (!type.IsValueType && !seen.Add(value)) { return; }
            result[key] = (result.TryGetValue(key, out long count) ? count : 0) + collection.Count;
            // 원소가 게임 코드의 물체면 그 속도 따라간다. 글자 목록은 글자 수도 센다.
            int taken = 0; long letters = 0;
            foreach (var element in collection)
            {
                if (element == null) { continue; }
                if (element is string text) { letters += text.Length; continue; }
                var kind = element.GetType();
                if (kind.IsPrimitive || kind.IsEnum || kind == typeof(decimal)) { break; }
                if (taken++ > 200000) { break; }
                if (kind.IsGenericType && kind.FullName.StartsWith("System.Collections.Generic.KeyValuePair"))
                {
                    object inner = kind.GetProperty("Value").GetValue(element);
                    Visit(inner, key + "[]", result, seen, queue, game);
                }
                else if (element is System.Collections.ICollection) { Visit(element, key + "[]", result, seen, queue, game); }
                else if (!kind.IsValueType && kind.Assembly == game && !(element is UnityEngine.Object) && seen.Add(element)) { queue.Enqueue(element); }
            }
            if (letters > 0) { result[key + " (chars)"] = (result.TryGetValue(key + " (chars)", out long chars) ? chars : 0) + letters; }
            return;
        }
        if (type.Assembly == game && !(value is UnityEngine.Object) && seen.Add(value)) { queue.Enqueue(value); }
    }
    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();
        bool IEqualityComparer<object>.Equals(object a, object b) => ReferenceEquals(a, b);
        int IEqualityComparer<object>.GetHashCode(object item) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item);
    }

    private static Dictionary<string, int> Count()
    {
        var result = new Dictionary<string, int>();
        foreach (var type in Counted) { result[type.Name] = Resources.FindObjectsOfTypeAll(type).Length; }
        return result;
    }

    /// <summary>재기 끝: 모인 프레임을 읽어 보고서를 쓴다.</summary>
    public static string End(string path, string label)
    {
        int last = ProfilerDriver.lastFrameIndex, first = Mathf.Max(ProfilerDriver.firstFrameIndex, last - 599);
        var self = new Dictionary<string, double>(); var total = new Dictionary<string, double>(); var alloc = new Dictionary<string, double>(); var calls = new Dictionary<string, double>();
        var frameTimes = new List<float>(); double gcPerFrame = 0; int frames = 0;
        var children = new List<int>(); var stack = new Stack<(int item, string parent)>();
        for (int frame = first; frame <= last; frame++)
        {
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnSelfTime, false))
            {
                if (view == null || !view.valid) { continue; }
                frames++; frameTimes.Add(view.frameTimeMs);
                stack.Clear(); stack.Push((view.GetRootItemID(), ""));
                while (stack.Count > 0)
                {
                    var entry = stack.Pop(); int item = entry.item;
                    string name = view.GetItemName(item);
                    float own = view.GetItemColumnDataAsFloat(item, HierarchyFrameDataView.columnSelfTime), all = view.GetItemColumnDataAsFloat(item, HierarchyFrameDataView.columnTotalTime);
                    float bytes = view.GetItemColumnDataAsFloat(item, HierarchyFrameDataView.columnGcMemory), count = view.GetItemColumnDataAsFloat(item, HierarchyFrameDataView.columnCalls);
                    if (!string.IsNullOrEmpty(name))
                    {
                        self[name] = (self.TryGetValue(name, out double a) ? a : 0) + own;
                        total[name] = (total.TryGetValue(name, out double b) ? b : 0) + all;
                        calls[name] = (calls.TryGetValue(name, out double c) ? c : 0) + count;
                        // GC는 그 함수가 직접 만든 것만 센다(자식 것을 빼서).
                        children.Clear(); view.GetItemChildren(item, children);
                        float childBytes = 0f;
                        foreach (int child in children) { childBytes += view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnGcMemory); }
                        float ownBytes = Mathf.Max(0f, bytes - childBytes);
                        // 쓰레기를 만든 자리는 "GC.Alloc"이라는 이름으로 찍힌다. 누가 만들었는지 알 수 있게 그 위의 함수 이름으로 적는다.
                        string owner = name == "GC.Alloc" ? entry.parent : name;
                        if (ownBytes > 0f) { alloc[owner] = (alloc.TryGetValue(owner, out double d) ? d : 0) + ownBytes; }
                        if (item == view.GetRootItemID()) { gcPerFrame += bytes; }
                    }
                    else { children.Clear(); view.GetItemChildren(item, children); }
                    foreach (int child in children) { stack.Push((child, name ?? "")); }
                }
            }
        }
        Profiler.enabled = false; ProfilerDriver.enabled = false;
        Application.targetFrameRate = savedRate; QualitySettings.vSyncCount = savedSync;
        GC.Collect();
        var censusAfter = Census();
        var after = Count(); long monoAfter = Profiler.GetMonoUsedSizeLong(), totalAfter = Profiler.GetTotalAllocatedMemoryLong();
        var text = new StringBuilder();
        text.AppendLine("== " + label + " ==");
        if (frames == 0) { text.AppendLine("no frames captured"); File.AppendAllText(path, text.ToString()); return text.ToString(); }
        frameTimes.Sort();
        float average = frameTimes.Average(), median = frameTimes[frameTimes.Count / 2], slow = frameTimes[Mathf.Min(frameTimes.Count - 1, (int)(frameTimes.Count * .95f))], worst = frameTimes[frameTimes.Count - 1];
        text.AppendLine($"frames {frames} over {UnityEditor.EditorApplication.timeSinceStartup - startedAt:0.0}s · frame ms: average {average:0.00} (fps {1000f / average:0.0}) median {median:0.00} p95 {slow:0.00} worst {worst:0.00}");
        text.AppendLine($"GC allocated per frame: {gcPerFrame / frames / 1024.0:0.0} KB");
        text.AppendLine($"mono heap {monoBefore / 1048576.0:0.0} MB -> {monoAfter / 1048576.0:0.0} MB · total allocated {totalBefore / 1048576.0:0.0} MB -> {totalAfter / 1048576.0:0.0} MB");
        text.AppendLine("object counts (before -> after):");
        foreach (var pair in after) { int old = before != null && before.TryGetValue(pair.Key, out int value) ? value : 0; text.AppendLine($"  {pair.Key,-14} {old,7} -> {pair.Value,7}  ({pair.Value - old:+0;-0;0})"); }
        text.AppendLine("top self time (ms per frame · share · calls per frame):");
        double all2 = self.Values.Sum();
        foreach (var pair in self.OrderByDescending(p => p.Value).Take(45))
        { text.AppendLine($"  {pair.Value / frames,7:0.000}  {pair.Value / all2 * 100,5:0.0}%  {calls[pair.Key] / frames,8:0.0}  {pair.Key}"); }
        text.AppendLine("top total time (ms per frame):");
        foreach (var pair in total.OrderByDescending(p => p.Value).Take(40))
        { text.AppendLine($"  {pair.Value / frames,7:0.000}  {pair.Key}"); }
        text.AppendLine("top GC allocation (bytes per frame, own):");
        foreach (var pair in alloc.OrderByDescending(p => p.Value).Take(30))
        { text.AppendLine($"  {pair.Value / frames,9:0}  {pair.Key}"); }
        text.AppendLine("collections that grew (class.field: before -> after):");
        foreach (var pair in censusAfter.OrderByDescending(p => p.Value - (censusBefore != null && censusBefore.TryGetValue(p.Key, out long old) ? old : 0)).Take(30))
        {
            long old = censusBefore != null && censusBefore.TryGetValue(pair.Key, out long value) ? value : 0;
            if (pair.Value - old <= 0) { break; }
            text.AppendLine($"  {pair.Value - old,9:+0}  {old,10} -> {pair.Value,10}  {pair.Key}");
        }
        text.AppendLine("largest collections:");
        foreach (var pair in censusAfter.OrderByDescending(p => p.Value).Take(30)) { text.AppendLine($"  {pair.Value,10}  {pair.Key}"); }
        text.AppendLine();
        File.AppendAllText(path, text.ToString());
        return text.ToString();
    }
}
