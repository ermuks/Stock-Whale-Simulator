using System.Collections.Generic;
using UnityEngine;

// 방의 배경음. Resources/Audio/Ambience의 소리를 층으로 겹쳐 튼다.
// 바닥 층(항상): 방 공기, 모니터·컴퓨터 소리(책상에서 난다), 형광등 소리(천장에서 난다). 길이가 서로 달라(30·23·17초) 같은 조합이 잘 돌아오지 않는다.
// 가끔: 배관 소리(왼쪽 벽), 건물 소리(천장). 40~110초에 한 번, 번갈아.
// 상황 층(서서히 바뀐다): 장중에는 낮은 긴장음, 어느 종목이 크게 달아오르면 급등·급락음, 장이 끝나면 밤 소리.
// 엔딩에서 문이 열리면 문 밖의 빛 소리가 한 번 난다(DoorExit가 부른다).
// 소리 파일이 없으면 아무것도 하지 않는다. 전체 음량은 설정의 음량(AudioListener.volume)을 그대로 따른다.
public sealed class RoomAmbience : MonoBehaviour
{
    public static RoomAmbience Instance { get; private set; }
    /// <summary>바닥 층이 준비됐는가. 준비됐으면 예전의 만들어 낸 방 소리(WorkroomAmbience)는 틀지 않는다.</summary>
    public static bool HasFiles => Resources.Load<AudioClip>("Audio/Ambience/RoomTone") != null;
    [Range(0f, 1f)] public float roomTone = .30f, monitors = .26f, lightBuzz = .14f, trading = .20f, surge = .30f, night = .30f, oneShot = .38f, ending = .75f;
    /// <summary>상황 층이 바뀌는 데 걸리는 시간(초).</summary>
    public float fadeSeconds = 3f;
    /// <summary>이 열기 이상인 종목이 있으면 급등·급락음을 켜고, 이 아래로 식으면 끈다.</summary>
    public float surgeHeatOn = 4f, surgeHeatOff = 2.2f;
    private sealed class Layer { public AudioSource source; public float target, full; }
    private readonly Dictionary<string, Layer> layers = new();
    private AudioSource pipes, building, endingSource;
    private float nextOccasional, nextStateCheck;
    private bool surging, pipesNext = true, placed;
    public string State { get; private set; } = "";

    void Awake()
    {
        Instance = this;
        Add("RoomTone", roomTone, false); Add("Monitors", monitors, true); Add("LightBuzz", lightBuzz, true);
        Add("Trading", trading, false); Add("Surge", surge, false); Add("Night", night, false);
        pipes = Source("Pipes", true, false); building = Source("Building", true, false); endingSource = Source("EndingLight", false, false);
        foreach (string always in new[] { "RoomTone", "Monitors", "LightBuzz" }) { if (layers.TryGetValue(always, out var layer)) { layer.target = layer.full; } }
        nextOccasional = Time.unscaledTime + Random.Range(25f, 60f);
    }
    void OnDestroy() { if (Instance == this) { Instance = null; } }

    private void Add(string name, float volume, bool spatial)
    {
        var source = Source(name, spatial, true);
        if (source != null) { layers[name] = new Layer { source = source, full = volume }; }
    }
    private AudioSource Source(string name, bool spatial, bool loop)
    {
        var clip = Resources.Load<AudioClip>("Audio/Ambience/" + name);
        if (clip == null) { return null; }
        var holder = new GameObject("Area :: Sound " + name); holder.transform.SetParent(transform, false);
        var source = holder.AddComponent<AudioSource>();
        source.clip = clip; source.loop = loop; source.playOnAwake = false; source.volume = 0f; source.dopplerLevel = 0f;
        // 방 안에서 나는 소리는 자리가 있다. 걸어가면 가까워지고 멀어진다. 완전히 한쪽 귀로만 들리지는 않게 조금 섞는다.
        source.spatialBlend = spatial ? .8f : 0f; source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 3f; source.maxDistance = 26f; source.spread = 60f;
        return source;
    }

    /// <summary>소리가 나는 자리를 방의 물건에 맞춘다. 방이 다 만들어진 뒤에 한 번 한다.</summary>
    private void Place()
    {
        var views = DeskViewController.Instance; var shell = RoomShell.Instance;
        if (views == null || !views.Ready || shell == null || !shell.Built) { return; }
        placed = true;
        var screen = views.ScreenFor(DeskViewTarget.Chart);
        Vector3 desk = screen != null ? screen.position : new Vector3(0f, 5f, 0f);
        if (layers.TryGetValue("Monitors", out var hum)) { hum.source.transform.position = desk; }
        if (layers.TryGetValue("LightBuzz", out var buzz)) { buzz.source.transform.position = new Vector3(desk.x, 8f, desk.z - 3f); }
        var drain = GameObject.Find("Area :: Left Drain");
        if (pipes != null) { pipes.transform.position = drain != null ? drain.transform.position : new Vector3(-4.4f, 5.8f, 2f); }
        if (building != null) { building.transform.position = new Vector3(desk.x + 3f, 8.2f, desk.z - 6f); }
        if (endingSource != null) { endingSource.transform.position = shell.DoorCenter; }
    }

    void Update()
    {
        if (!placed) { Place(); }
        float now = Time.unscaledTime;
        if (now >= nextStateCheck) { nextStateCheck = now + 1f; ReadState(); }
        float step = Time.unscaledDeltaTime / Mathf.Max(.1f, fadeSeconds);
        foreach (var layer in layers.Values)
        {
            var source = layer.source;
            if (layer.target > 0f && !source.isPlaying) { source.Play(); }
            source.volume = Mathf.MoveTowards(source.volume, layer.target, step * Mathf.Max(layer.full, .05f));
            if (layer.target <= 0f && source.volume <= 0f && source.isPlaying) { source.Stop(); }
        }
        // 가끔 나는 소리: 배관과 건물을 번갈아. 엔딩 연출 중에는 내지 않는다.
        if (now >= nextOccasional && !RoomShell.Cinematic)
        {
            nextOccasional = now + Random.Range(40f, 110f);
            var source = pipesNext ? pipes : building; pipesNext = !pipesNext;
            if (source != null && !source.isPlaying) { source.volume = oneShot * Random.Range(.7f, 1f); source.pitch = Random.Range(.94f, 1.05f); source.Play(); }
        }
    }

    /// <summary>지금 상황에 맞는 층을 고른다: 장중, 급등·급락, 밤.</summary>
    private void ReadState()
    {
        var market = MarketDayController.Instance;
        bool open = market != null && market.Ready && market.Phase == MarketPhase.Trading, closed = market != null && market.Ready && market.Phase == MarketPhase.PostClose;
        float hottest = 0f;
        if (open && StockInterestManager.Instance != null && StockManager.Instance != null)
        { foreach (var stock in StockManager.Instance.stocks) { if (stock != null && stock.AcceptsOrders) { hottest = Mathf.Max(hottest, StockInterestManager.Instance.Heat(stock)); } } }
        surging = open && (surging ? hottest >= surgeHeatOff : hottest >= surgeHeatOn);
        State = surging ? "Surge" : open ? "Trading" : closed ? "Night" : "";
        Target("Surge", surging); Target("Trading", open && !surging); Target("Night", closed);
    }
    private void Target(string name, bool on)
    {
        if (layers.TryGetValue(name, out var layer)) { layer.target = on ? layer.full : 0f; }
    }

    /// <summary>문이 열리고 빛이 쏟아진다(엔딩). 다른 상황 층은 잦아든다.</summary>
    public void PlayEnding()
    {
        if (endingSource == null) { return; }
        endingSource.volume = ending; endingSource.Play();
    }
    /// <summary>꿈에서 깼다. 빛 소리를 끊는다.</summary>
    public void StopEnding()
    {
        if (endingSource != null) { endingSource.Stop(); }
    }
    /// <summary>확인용: 층마다 지금 음량과 재생 여부.</summary>
    public string Describe()
    {
        var text = new System.Text.StringBuilder("state " + (State == "" ? "none" : State) + " |");
        foreach (var pair in layers) { text.Append(" " + pair.Key + " " + pair.Value.source.volume.ToString("F2") + (pair.Value.source.isPlaying ? "▶" : "■") + " " + pair.Value.source.clip.length.ToString("F1") + "s " + pair.Value.source.clip.loadState + " |"); }
        text.Append(" pipes " + (pipes != null) + " building " + (building != null) + " ending " + (endingSource != null));
        return text.ToString();
    }
}
