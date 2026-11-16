using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit video preview of the production shader; no scene/save or gameplay tuning writes.</summary>
public static class FatigueVideoCapture
{
    private const int Width = 1280, Height = 720, Rate = 20, Frames = 720;
    private static string output, priorLanguage;
    private static bool hadLanguage, oldOptions, spawned, finished, prepared;
    private static EnterPlayModeOptions oldFlags;
    private static double began;
    private static int frame, phase;
    private static float due;
    private static Camera camera;
    private static RenderTexture target;
    private static Texture2D pixels;
    private static RawImage effect;
    private static TextMeshProUGUI caption, number;
    private static Image meter;
    private static Vector3 position;
    private static Quaternion rotation;
    private static int errors;
    [MenuItem("DonDon/Preview/Capture fatigue video")]
    public static void Run()
    {
        output = Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if (string.IsNullOrEmpty(output) || !Path.GetFullPath(output).StartsWith(Path.GetFullPath(".")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Use the bounded Settings runner with an output folder in this workspace."); }
        Directory.CreateDirectory(Path.Combine(output,"frames"));
        hadLanguage = PlayerPrefs.HasKey("DonDon.Language"); priorLanguage = PlayerPrefs.GetString("DonDon.Language");
        File.WriteAllText(Path.Combine(output,"language-before.txt"),(hadLanguage ? "1\n" : "0\n")+priorLanguage);
        PlayerPrefs.SetString("DonDon.Language","ko"); PlayerPrefs.Save();
        FatigueBlurValidation.Install();
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup; EditorApplication.update += Monitor; Application.logMessageReceived += Log;
        EditorApplication.EnterPlaymode();
    }
    private static void Monitor()
    {
        if (finished) { return; }
        if (EditorApplication.timeSinceStartup-began > 230) { Finish("Time budget exceeded"); return; }
        if (EditorApplication.isPlaying && !spawned)
        { spawned = true; new GameObject("Preview :: Fatigue Capture").AddComponent<FatigueVideoProbe>(); }
    }
    private static void Log(string message,string stack,LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) { return; }
        errors++; File.AppendAllText(Path.Combine(output,"errors.txt"),message+"\n"+stack+"\n");
    }
    public static void Step()
    {
        if (finished || !GameLocale.Ready || Time.unscaledTime < due) { return; }
        try
        {
            if (phase == 0)
            {
                var setup = Object.FindAnyObjectByType<PlayerCompanySetupUI>();
                if (setup == null || setup.businessName == null) { return; }
                setup.businessName.SetTextWithoutNotify("플레이어 기업"); setup.Refresh(); setup.confirm.onClick.Invoke();
                phase = 1; due = Time.unscaledTime + 2; return;
            }
            if (phase == 1)
            {
                Time.timeScale = 1;
                if (MarketDayController.Instance == null || !MarketDayController.Instance.Ready || MarketDayController.Instance.Phase != MarketPhase.Trading)
                { return; }
                StockManager.Instance.SelectStock(1); phase = 2; due = Time.unscaledTime + .8f; return;
            }
            if (phase == 2) { Prepare(); phase = 3; due = Time.unscaledTime + .2f; return; }
            RenderFrame();
            if (++frame >= Frames) { Finish(errors == 0 ? null : "Render errors: "+errors); }
        }
        catch (Exception ex) { Finish(ex.ToString()); }
    }
    private static void Prepare()
    {
        camera = Camera.main; position = camera.transform.position; rotation = camera.transform.rotation;
        Time.timeScale = 0;
        var rest = PlayerRestController.Instance; rest.enabled = false;
        effect = rest.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "Image :: Tired Eyes");
        FatigueBlurValidation.Verify(camera);
        // Use the exact shipping material, including its slow breathing formula and opacity limits.
        foreach (var canvas in Object.FindObjectsByType<Canvas>())
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) { continue; }
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .1f;
        }
        var root = new GameObject("Preview :: Video Annotation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        var overlay = root.GetComponent<Canvas>(); overlay.renderMode = RenderMode.ScreenSpaceCamera; overlay.worldCamera = camera;
        overlay.planeDistance = camera.nearClipPlane+.1f; overlay.sortingOrder = 31000;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(Width,Height); scaler.matchWidthOrHeight = 1;
        var strip = new GameObject("Preview :: Caption Strip",typeof(RectTransform),typeof(Image)); strip.transform.SetParent(root.transform,false);
        var rect = strip.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0,0); rect.anchorMax = new Vector2(1,0); rect.pivot = Vector2.zero;
        rect.sizeDelta = new Vector2(0,58); rect.anchoredPosition = Vector2.zero;
        strip.GetComponent<Image>().color = new Color(.015f,.02f,.025f,.88f);
        caption = Label(strip.transform,"정상 시야",24,18,900,30,20);
        number = Label(strip.transform,"피로 0%",1060,18,192,30,20); number.alignment = TextAlignmentOptions.MidlineRight;
        var gauge = new GameObject("Preview :: Fatigue Meter",typeof(RectTransform),typeof(Image)); gauge.transform.SetParent(strip.transform,false);
        var gaugeRect = gauge.GetComponent<RectTransform>(); gaugeRect.anchorMin = gaugeRect.anchorMax = gaugeRect.pivot = Vector2.zero;
        gaugeRect.anchoredPosition = new Vector2(24,9); gaugeRect.sizeDelta = new Vector2(1232,2);
        meter = gauge.GetComponent<Image>(); meter.color = new Color(.75f,.8f,.84f,.8f);
        target = new RenderTexture(Width,Height,24) { filterMode = FilterMode.Bilinear };
        target.Create(); pixels = new Texture2D(Width,Height,TextureFormat.RGB24,false);
        camera.targetTexture = target; prepared = true;
        File.WriteAllText(Path.Combine(output,"capture-info.txt"),"1280x720 / 20 fps / 36 seconds\nProduction FatigueVision + FatigueBlurFeature on actual TradeScene camera.\nMarket frozen; fatigue progression is preview-only, not gameplay acceleration.\nGaussian radius <=2 output pixels; camera shutter span <=4 output pixels.\n0-4s clear; 4-10s 0->60; 10-16s 60->100; 18-28s gentle camera rotation; 31-36s recovery.\n");
    }
    private static TextMeshProUGUI Label(Transform parent,string text,float x,float y,float width,float height,float size)
    {
        var go = new GameObject("Preview :: "+text,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x,y); rect.sizeDelta = new Vector2(width,height);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = Resources.Load<TMP_FontAsset>("UI/News/Pretendard Reading");
        label.fontSize = size; label.color = new Color(.9f,.93f,.95f); label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false; label.text = text; return label;
    }
    private static float Fatigue(float time)
    {
        if (time < 4) { return 0; }
        if (time < 10) { return Mathf.SmoothStep(0,60,(time-4)/6); }
        if (time < 16) { return Mathf.SmoothStep(60,100,(time-10)/6); }
        if (time < 31) { return 100; }
        return Mathf.SmoothStep(100,0,(time-31)/4);
    }
    private static void RenderFrame()
    {
        float time = frame/(float)Rate, fatigue = Fatigue(time);
        float yaw = time >= 18 && time < 28 ? 6f * Mathf.Sin((time-18) * Mathf.PI / 5f) : 0;
        camera.transform.SetPositionAndRotation(position,rotation * Quaternion.Euler(0,yaw,0)); Time.timeScale = 0;
        PlayerRestController.Instance.State.fatigue = fatigue;
        effect.enabled = fatigue > 25; effect.material.SetFloat("_Fatigue",fatigue/100);
        effect.material.SetFloat("_Breath",.5f+.5f*Mathf.Sin(time*.5f));
        caption.text = time < 4 ? "정상 시야" : time < 10 ? "피로 누적 · 화면 전체가 아주 조금 흐려짐" : time < 18 ?
            "최대 2px 가우시안 블러 · 느린 눈꺼풀 움직임" : time < 28 ? "시선을 돌릴 때만 은은한 모션 블러" : time < 31 ? "시선 정지 · 모션 블러 해제" : "회복 후 시야 비교";
        number.text = "피로 "+Mathf.RoundToInt(fatigue)+"%";
        meter.rectTransform.sizeDelta = new Vector2(1232*fatigue/100,2);
        Canvas.ForceUpdateCanvases();
        var old = RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,Width,Height),0,0); pixels.Apply(false);
            File.WriteAllBytes(Path.Combine(output,"frames",frame.ToString("D4")+".jpg"),pixels.EncodeToJPG(92));
            if (frame % 20 == 0)
            {
                var optics = FatigueBlurValidation.CurrentFeature();
                File.AppendAllText(Path.Combine(output,"optics.csv"),time.ToString("F2")+","+optics.LastGaussianRadius.ToString("F4")+","+optics.LastMotionPixels.magnitude.ToString("F4")+"\n");
            }
            if (frame % 100 == 0) { File.WriteAllText(Path.Combine(output,"progress.txt"),frame+" / "+Frames); }
        }
        finally { RenderTexture.active = old; }
    }
    private static void Finish(string failure)
    {
        if (finished) { return; } finished = true;
        if (prepared)
        {
            camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(pixels);
        }
        if (hadLanguage) { PlayerPrefs.SetString("DonDon.Language",priorLanguage); } else { PlayerPrefs.DeleteKey("DonDon.Language"); }
        PlayerPrefs.Save();
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions; EditorSettings.enterPlayModeOptions = oldFlags;
        File.WriteAllText(Path.Combine(output,"result.txt"),(failure ?? "PASS")+"\nFrames: "+frame+"\nPreferences restored.\n");
        EditorApplication.update -= Monitor; Application.logMessageReceived -= Log; EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
[DefaultExecutionOrder(32000)]
public sealed class FatigueVideoProbe : MonoBehaviour
{
    private void Awake() { Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 20; }
    private void LateUpdate() { FatigueVideoCapture.Step(); }
}
