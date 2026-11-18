using UnityEngine;

/// <summary>
/// 어두운 독방의 조명과 작은 팬/빗소리. 카메라와 화면 Transform은 변경하지 않는다.
/// 방의 등과 주변 빛을 낮춰서 화면 둘레만 겨우 보이게 하고, 전등은 낡아서 가끔 깜빡 꺼질 듯 흔들린다.
/// 씬에 저장된 값은 그대로 두고 실행할 때만 낮춘다.
/// </summary>
public sealed class WorkroomAmbience : MonoBehaviour
{
    public Light practical;
    [Range(0f, .3f)] public float ambientVolume = .07f;
    /// <summary>방 안 등의 밝기에 곱하는 값, 주변 빛에 곱하는 값, 안개 짙기에 곱하는 값.</summary>
    [Range(0f, 1f)] public float lampDim = .22f, ambientDim = .30f;
    [Range(1f, 4f)] public float fogThicken = 1.9f;
    private AudioSource source;
    private AudioClip roomTone;
    private float baseIntensity, dip, nextDip;
    void Start()
    {
        Darken();
        if (practical != null)
        { baseIntensity = practical.intensity; }
        nextDip = Time.unscaledTime + 8f;
        // 녹음된 방 소리(RoomAmbience)가 있으면 여기서 만들어 내던 소리는 틀지 않는다.
        if (RoomAmbience.HasFiles) { return; }
        source = gameObject.AddComponent<AudioSource>();
        const int rate = 22050, length = rate * 4;
        var samples = new float[length];
        var noise = new System.Random(751);
        float smooth = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float seconds = i / (float)rate;
            smooth = smooth * .97f + ((float)noise.NextDouble() * 2f - 1f) * .03f;
            float edge = Mathf.Min(1f, Mathf.Min(i, length - 1 - i) / 1100f);
            samples[i] = edge * (smooth * .6f + Mathf.Sin(seconds * 2f * Mathf.PI * 60f) * .025f);
        }
        roomTone = AudioClip.Create("Workroom fan and rain", length, 1, rate, false);
        roomTone.SetData(samples, 0);
        source.clip = roomTone;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = ambientVolume;
        source.Play();
    }
    /// <summary>방의 등, 주변 빛, 안개를 독방처럼 어둡게 맞춘다. 화면에서 나오는 빛과 시계의 작은 불빛은 건드리지 않는다.</summary>
    private void Darken()
    {
        foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            // 화면 빛은 따로 세기를 정하고, 닿는 거리가 아주 짧은 것은 시계 숫자 같은 작은 불빛이다.
            if (light.name.StartsWith("Light :: Screen Glow") || light.name.Contains("Monitor") || light.range <= 1.6f) { continue; }
            light.intensity *= lampDim;
        }
        RenderSettings.ambientLight *= ambientDim;
        RenderSettings.fogDensity *= fogThicken;
        RenderSettings.fogColor *= .55f;
    }
    void Update()
    {
        if (practical == null) { return; }
        // 평소에는 아주 느리게 일렁이고, 가끔 접촉이 나쁜 전등처럼 잠깐 푹 꺼졌다 돌아온다.
        float now = Time.unscaledTime;
        if (now >= nextDip) { dip = 1f; nextDip = now + Random.Range(14f, 46f); }
        dip = Mathf.Max(0f, dip - Time.unscaledDeltaTime * 2.2f);
        float stutter = dip > 0f ? Mathf.Lerp(1f, .15f + .5f * Mathf.PerlinNoise(now * 31f, 3.1f), dip) : 1f;
        practical.intensity = baseIntensity * (.93f + .07f * Mathf.PerlinNoise(now * .23f, .7f)) * stutter;
    }
    void OnDestroy()
    {
        if (source != null) { source.Stop(); }
        if (roomTone != null) { Destroy(roomTone); }
    }
}
