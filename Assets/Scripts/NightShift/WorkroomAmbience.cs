using UnityEngine;

/// <summary>어두운 작업실의 느린 전등 변화와 작은 팬/빗소리. 카메라와 화면 Transform은 변경하지 않는다.</summary>
public sealed class WorkroomAmbience : MonoBehaviour
{
    public Light practical;
    [Range(0f, .3f)] public float ambientVolume = .07f;
    private AudioSource source;
    private AudioClip roomTone;
    private float baseIntensity;
    void Start()
    {
        if (practical != null)
        { baseIntensity = practical.intensity; }
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
    void Update()
    {
        if (practical != null)
        { practical.intensity = baseIntensity * (.96f + .04f * Mathf.PerlinNoise(Time.unscaledTime * .23f, .7f)); }
    }
    void OnDestroy()
    {
        if (source != null) { source.Stop(); }
        if (roomTone != null) { Destroy(roomTone); }
    }
}
