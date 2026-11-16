using UnityEngine;
using UnityEngine.UI;

/// <summary>Fatigue clock and eyelids. FatigueBlurFeature handles the player camera's GPU optics.</summary>
public sealed class PlayerRestController : MonoBehaviour
{
    public static PlayerRestController Instance { get; private set; }
    public PlayerRestState State = new();
    [Min(0)] public float fatiguePerGameHour = 2.5f;
    private MarketDayController day;
    private Material vision;
    private RawImage overlay;
    private float nextRefresh;
    public Camera ViewCamera { get; private set; }
    public bool CanSleep => day != null && day.Ready && day.Phase == MarketPhase.PostClose && !State.dead;
    public double SleepHours => CanSleep ? (1920d - day.ExactGameMinute) / 60d : 0d;
    private void Awake()
    {
        Instance = this; day = GetComponent<MarketDayController>(); ViewCamera = Camera.main;
        var root = new GameObject("Area :: Fatigue Vision", typeof(RectTransform), typeof(Canvas)); root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 29900;
        var graphic = new GameObject("Image :: Tired Eyes", typeof(RectTransform), typeof(RawImage)); graphic.transform.SetParent(root.transform, false);
        overlay = graphic.GetComponent<RawImage>(); overlay.raycastTarget = false;
        var rect = overlay.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var shader = Resources.Load<Shader>("UI/SystemMenu/FatigueVision");
        if (shader != null) { vision = new Material(shader); overlay.material = vision; }
        overlay.enabled = false;
    }
    public void ObserveClock()
    {
        if (day == null || !day.Ready) { return; }
        State.Advance(day.AbsoluteGameMinute, fatiguePerGameHour);
    }
    private void Update()
    {
        if (Time.timeScale > 0) { ObserveClock(); }
        if (Time.unscaledTime < nextRefresh) { return; }
        nextRefresh = Time.unscaledTime + .05f;
        bool shown = vision != null && State.fatigue > 25 && !PauseMenuController.IsOpen;
        overlay.enabled = shown;
        if (!shown) { return; }
        vision.SetFloat("_Fatigue", (float)State.fatigue / 100f);
        // Slow eyelid motion, never rapid flashing or a change to the user's camera pose.
        vision.SetFloat("_Breath", .5f + .5f * Mathf.Sin(SessionClock.Time * .5f));
    }
    public bool SleepUntilMorning()
    {
        if (!CanSleep) { return false; }
        ObserveClock(); double hours = SleepHours;
        if (hours <= 0) { return false; }
        State.Sleep(hours, (day.Day - 1) * 1440d + 1920d);
        day.StartMorningAfterSleep(); return true;
    }
    public void NightPassedAwake()
    {
        ObserveClock(); State.MissNight();
        if (State.dead) { PauseMenuController.Instance?.ShowDeath(); }
    }
    private void OnDestroy()
    { if (vision != null) { Destroy(vision); } if (Instance == this) { Instance = null; } }
}
