using System;
using UnityEngine;

/// <summary>Local preferences, independent of a market session. Only implemented controls are exposed.</summary>
[DefaultExecutionOrder(-19900)]
public sealed class GameSettings : MonoBehaviour
{
    [Serializable] public sealed class Values
    {
        public int version = 1, frameRate = 60, quality = 1;
        public int marketColors;
        public bool vSync, fullscreen = true, muteUnfocused = true;
        public float volume = .8f, sensitivity = 1f, wheel = 3f, menuScale = 1f;
        /// <summary>태블릿을 들고 볼 때 화면이 시야에서 차지하는 크기(0.30~0.90). 뒤의 모니터가 가려지지 않게 기본은 작게 둔다.
        /// (처음에는 tabletSize라는 이름에 기본 0.60이었다. 모니터를 다 가려서 기본을 줄였고, 이미 저장된 0.60이 따라오지 않게 이름을 바꿨다.)</summary>
        public float tabletView = .46f;
    }
    public const string Preference = "DonDon.Settings.v1";
    public static Values Current { get; private set; } = new();
    public static event Action Changed;
    private bool focused = true;
    private float saveAt = -1;
    private static GameSettings instance;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { instance = null; Current = new(); Changed = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var root = new GameObject("System :: Preferences"); DontDestroyOnLoad(root);
        instance = root.AddComponent<GameSettings>();
        try { Current = JsonUtility.FromJson<Values>(PlayerPrefs.GetString(Preference,"")) ?? new Values(); }
        catch (ArgumentException) { Current = new Values(); }
        if (Current.version != 1) { Current = new Values(); }
        Clamp(); Apply();
    }
    private static float Valid(float value,float fallback,float min,float max)
    { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value,min,max); }
    private static void Clamp()
    {
        if (Current.marketColors!=MarketColors.RedBlue && Current.marketColors!=MarketColors.GreenRed) { Current.marketColors=MarketColors.RedBlue; }
        Current.quality = Mathf.Clamp(Current.quality,0,Math.Max(0,QualitySettings.names.Length-1));
        // 0은 제한 없음.
        if (Current.frameRate != 0 && Current.frameRate != 30 && Current.frameRate != 60 && Current.frameRate != 120) { Current.frameRate = 60; }
        Current.volume = Valid(Current.volume,.8f,0,1); Current.sensitivity = Valid(Current.sensitivity,1,.25f,2.5f);
        Current.wheel = Valid(Current.wheel,3,1,6); Current.menuScale = Valid(Current.menuScale,1,.9f,1.1f);
        Current.tabletView = Current.tabletView <= 0f ? .46f : Valid(Current.tabletView,.46f,.3f,.9f);
    }
    public static void Commit()
    {
        Clamp(); Apply(); PlayerPrefs.SetString(Preference,JsonUtility.ToJson(Current));
        if (instance != null) { instance.saveAt = Time.unscaledTime+.35f; } else { PlayerPrefs.Save(); }
        Changed?.Invoke();
    }
    public static void Defaults() { Current = new Values(); Commit(); }
    private static void Apply()
    {
        MarketColors.Apply();
        // Avoid expensive pipeline/OS switches on every slider movement.
        if (QualitySettings.GetQualityLevel() != Current.quality) { QualitySettings.SetQualityLevel(Current.quality,true); }
        QualitySettings.vSyncCount = Current.vSync ? 1 : 0; Application.targetFrameRate = Current.frameRate <= 0 ? -1 : Current.frameRate;
        if (SessionStore.ValidationMode) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 20; }
        if (!Application.isEditor)
        {
            var mode = Current.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Screen.fullScreenMode != mode) { Screen.fullScreenMode = mode; }
        }
        AudioListener.volume = Current.muteUnfocused && instance != null && !instance.focused ? 0 : Current.volume;
    }
    private void OnApplicationFocus(bool value) { focused = value; Apply(); }
    private void Update()
    { if (saveAt >= 0 && Time.unscaledTime >= saveAt) { saveAt = -1; PlayerPrefs.Save(); } }
    private void OnApplicationQuit() { PlayerPrefs.Save(); }
}
