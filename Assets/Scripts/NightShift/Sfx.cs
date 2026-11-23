using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 효과음. Resources/Audio/Effects의 짧은 소리를 이름으로 튼다: Sfx.Play("DoorOpen").
// 같은 이름에 _1, _2 …가 붙은 파일이 있으면 그중 하나를 고른다(발소리, 잠긴 문). 직전에 낸 것은 다시 고르지 않는다.
// 자리를 주면 그 자리에서 나고(걸어가면 가까워진다), 주지 않으면 화면 소리로 난다.
// 파일이 없는 이름은 조용히 넘어간다. 나중에 파일만 넣으면 소리가 난다.
// 화면의 모든 단추에는 누르는 소리를 붙인다(새로 생기는 단추도 2초마다 찾아 붙인다).
public sealed class Sfx : MonoBehaviour
{
    public static Sfx Instance { get; private set; }
    private const int Voices = 10;
    private const float ButtonScanSeconds = 2f;
    private readonly AudioSource[] voices = new AudioSource[Voices];
    private readonly Dictionary<string, AudioClip[]> clips = new();
    private readonly Dictionary<string, int> lastPick = new();
    private readonly Dictionary<string, float> lastPlayed = new();
    private readonly HashSet<Button> wired = new();
    private int cursor;
    private float nextScan;
    /// <summary>확인용: 이름별로 몇 번 틀었는가.</summary>
    public readonly Dictionary<string, int> Played = new();
    public int ButtonsWired => wired.Count;

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < Voices; i++)
        {
            var holder = new GameObject("Area :: Sound Effect " + i); holder.transform.SetParent(transform, false);
            var source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.dopplerLevel = 0f; source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 3f; source.maxDistance = 30f; source.spread = 60f;
            voices[i] = source;
        }
    }
    void OnEnable() { Trader.OrderRefused += Refused; }
    void OnDisable() { Trader.OrderRefused -= Refused; }
    void OnDestroy() { if (Instance == this) { Instance = null; } }
    /// <summary>내 주문이 돈이나 수량이 모자라 나가지 못했을 때만 실패음을 낸다. 봇의 주문은 듣지 않는다. 자동 주문이 되풀이돼도 0.6초에 한 번만 난다.</summary>
    private static void Refused(Trader trader, bool buy)
    {
        if (PlayerAccounts.IsPlayer(trader)) { Play("UiDeny", .7f, null, .02f, .6f); }
    }

    private AudioClip[] Clips(string name)
    {
        if (clips.TryGetValue(name, out var found)) { return found; }
        var list = new List<AudioClip>();
        var single = Resources.Load<AudioClip>("Audio/Effects/" + name);
        if (single != null) { list.Add(single); }
        for (int i = 1; i <= 8; i++)
        {
            var variant = Resources.Load<AudioClip>("Audio/Effects/" + name + "_" + i);
            if (variant == null) { break; }
            list.Add(variant);
        }
        return clips[name] = list.ToArray();
    }
    /// <summary>이 이름의 소리를 미리 읽어 둔다. 처음 틀 때 파일을 읽고 푸느라 화면이 멎는 것을 막는다.</summary>
    public static void Warm(string name)
    {
        if (Instance == null) { return; }
        foreach (var clip in Instance.Clips(name)) { if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) { clip.LoadAudioData(); } }
    }
    /// <summary>이 이름의 소리 파일이 몇 개 있는가.</summary>
    public int Count(string name) => Clips(name).Length;

    /// <summary>
    /// 효과음을 튼다. at을 주면 그 자리에서 난다. pitchJitter만큼 음높이를 흔들어 같은 소리가 되풀이되는 느낌을 줄인다.
    /// minGap(초) 안에 같은 이름을 다시 부르면 무시한다(연속 체결처럼 한꺼번에 몰리는 소리).
    /// </summary>
    public static void Play(string name, float volume = 1f, Vector3? at = null, float pitchJitter = .04f, float minGap = 0f)
    {
        var player = Instance;
        if (player == null) { return; }
        var set = player.Clips(name);
        if (set.Length == 0) { return; }
        float now = Time.unscaledTime;
        if (minGap > 0f && player.lastPlayed.TryGetValue(name, out float last) && now - last < minGap) { return; }
        player.lastPlayed[name] = now;
        int pick = 0;
        if (set.Length > 1)
        {
            player.lastPick.TryGetValue(name, out int previous);
            pick = Random.Range(0, set.Length - 1); if (pick >= previous) { pick++; }
            player.lastPick[name] = pick;
        }
        // 쉬고 있는 목소리를 찾고, 없으면 가장 오래된 것을 끊고 쓴다.
        AudioSource source = null;
        for (int i = 0; i < Voices; i++) { var candidate = player.voices[(player.cursor + i) % Voices]; if (!candidate.isPlaying) { source = candidate; player.cursor = (player.cursor + i + 1) % Voices; break; } }
        if (source == null) { source = player.voices[player.cursor]; player.cursor = (player.cursor + 1) % Voices; source.Stop(); }
        source.clip = set[pick]; source.volume = Mathf.Clamp01(volume); source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.spatialBlend = at.HasValue ? .8f : 0f;
        if (at.HasValue) { source.transform.position = at.Value; }
        source.Play();
        player.Played.TryGetValue(name, out int count); player.Played[name] = count + 1;
    }

    void Update()
    {
        if (Time.unscaledTime < nextScan) { return; }
        nextScan = Time.unscaledTime + ButtonScanSeconds;
        if (Count("UiClick") == 0) { return; }
        // 사라진 단추는 잊는다.
        wired.RemoveWhere(button => button == null);
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (!wired.Add(button)) { continue; }
            button.onClick.AddListener(Click);
        }
    }
    private static void Click() { Play("UiClick", .55f, null, .05f, .04f); }

    public string Describe()
    {
        var text = new System.Text.StringBuilder("buttons " + ButtonsWired + " |");
        foreach (string name in new[] { "UiClick", "UiDeny", "OrderFill", "NoteKnock", "NoteSlide", "NotePick", "NoteStick", "DoorLocked", "DoorUnlock", "DoorOpen", "DrawerOpen", "DrawerClose", "BedDown", "BedUp", "Footstep" })
        { Played.TryGetValue(name, out int count); text.Append(" " + name + " files " + Count(name) + " played " + count + " |"); }
        return text.ToString();
    }
}
