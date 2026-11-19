using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아침 브리핑에서 적어 두는 포스트잇 두 장: 오를 것으로 보이는 종목 한 장, 내릴 것으로 보이는 종목 한 장.
/// 모니터 아래, 할 일 메모의 왼쪽에 붙는다. 잠들면 떨어져 책상 위에 쌓인다(치우지 않는다. 책상을 어지르는 쓰레기가 된다).
/// 종이는 할 일 메모와 같은 것을 찍어 쓴다. 게임 상태는 건드리지 않는다.
/// </summary>
public sealed class MemoNotes : MonoBehaviour
{
    public static MemoNotes Instance { get; private set; }
    /// <summary>책상 위에 남겨 두는 떨어진 메모의 수. 넘치면 가장 오래된 것부터 치운다.</summary>
    private const int Kept = 14;
    /// <summary>모니터 밑변 왼쪽 끝 자리: 할 일 메모의 첫 장에서 왼쪽으로 몇 장 간격인지(0이 바로 옆).</summary>
    private const float LeftPlace = 1.85f;
    private readonly List<InvestorJourneyNotes.Note> stuck = new();
    private readonly Queue<GameObject> dropped = new();
    public int Stuck => stuck.Count;
    public int Dropped => dropped.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { Instance = null; }
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    private static string Word(string korean, string english, string japanese) => GameLocale.Code == "ja" ? japanese : GameLocale.Code == "en" ? english : korean;

    /// <summary>브리핑의 기사들을 두 장에 나눠 적어 붙인다. 붙어 있던 지난 메모는 먼저 떨어진다.</summary>
    public bool Save(IReadOnlyList<MorningBriefing.Entry> entries)
    {
        var board = FindAnyObjectByType<InvestorJourneyNotes>();
        if (board == null || entries == null || entries.Count == 0) { return false; }
        DropAll();
        var rising = new List<MorningBriefing.Entry>(); var falling = new List<MorningBriefing.Entry>();
        foreach (var entry in entries) { (entry.Expected >= 0f ? rising : falling).Add(entry); }
        // 모니터 밑변의 맨 왼쪽 두 자리. 오를 것은 초록 종이(셋째 원본), 내릴 것은 주황 종이(둘째 원본).
        if (rising.Count > 0) { Write(board.SpawnMemo(2, LeftPlace), Word("오를 것", "Going up", "上がりそう"), rising); }
        if (falling.Count > 0) { Write(board.SpawnMemo(1, LeftPlace - 1f), Word("내릴 것", "Going down", "下がりそう"), falling); }
        Debug.Log("[메모] 포스트잇 저장: 오를 것 " + rising.Count + "종목 · 내릴 것 " + falling.Count + "종목 · 붙은 메모 " + stuck.Count + "장");
        return stuck.Count > 0;
    }

    private void Write(InvestorJourneyNotes.Note note, string heading, List<MorningBriefing.Entry> list)
    {
        if (note == null || note.paper == null) { return; }
        var day = MarketDayController.Instance;
        Put(note.number, day == null ? "" : "D" + day.Day);
        Put(note.title, heading);
        Put(note.detail, Line(list, 0));
        Put(note.footer, Line(list, 1));
        Put(note.status, list.Count > 2 ? "+" + (list.Count - 2) : "");
        stuck.Add(note);
    }

    private static string Line(List<MorningBriefing.Entry> list, int index)
    { return index >= list.Count ? "" : list[index].Stock.symbol + " " + list[index].Expected.ToString("+0.0;-0.0;0.0") + "%"; }

    private static void Put(TMPro.TextMeshProUGUI label, string text)
    { if (label != null) { GameLocalizedText.Set(label, text, true); } }

    /// <summary>붙어 있는 메모를 모두 떨어뜨린다(잠들 때). 떨어진 종이는 책상에 남는다.</summary>
    public void DropAll()
    {
        float delay = .15f;
        foreach (var note in stuck)
        {
            if (note == null || note.paper == null) { continue; }
            note.paper.Release(delay); delay += .4f;
            dropped.Enqueue(note.paper.gameObject);
        }
        stuck.Clear();
        while (dropped.Count > Kept) { var old = dropped.Dequeue(); if (old != null) { Destroy(old); } }
    }
}
