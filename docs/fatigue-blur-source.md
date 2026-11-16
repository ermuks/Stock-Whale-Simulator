# 피로 블러 신규 소스와 함수별 변경

2026-11-15. 이번 변경의 신규 소스 전문과 기존 함수 전후 비교입니다.

## Assets/Scripts/Settings/FatigueBlurFeature.cs

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>Player-camera optics only. Pixel limits refer to final output, not render scale.</summary>
public sealed class FatigueBlurFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    [Range(0, 2)] public float maximumGaussianRadius = 2f;
    [Range(0, 4)] public float maximumMotionPixels = 4f;
    private Material material;
    private OpticsPass pass;
    private readonly MotionHistory history = new();
    public float LastGaussianRadius { get; private set; }
    public Vector2 LastMotionPixels { get; private set; }
    public int RecordedPasses { get; private set; }

    public override void Create()
    {
        CoreUtils.Destroy(material);
        if (shader == null) { shader = Resources.Load<Shader>("UI/SystemMenu/FatigueBlur"); }
        if (shader != null) { material = CoreUtils.CreateEngineMaterial(shader); }
        pass = new OpticsPass(this) { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
        history.Reset();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var camera = renderingData.cameraData.camera;
        var rest = PlayerRestController.Instance;
        if (!Application.isPlaying || camera.cameraType != CameraType.Game || rest == null || camera != rest.ViewCamera)
        { return; }
        LastGaussianRadius = 0; LastMotionPixels = Vector2.zero;
        if (PauseMenuController.IsOpen || material == null)
        { history.Reset(); return; }
        float fatigue = (float)rest.State.fatigue;
        LastGaussianRadius = GaussianRadius(fatigue, maximumGaussianRadius);
        if (DeskViewController.Instance != null && DeskViewController.Instance.IsTransitioning) { history.Reset(); }
        else { LastMotionPixels = history.Sample(camera, fatigue, maximumMotionPixels, Time.unscaledTimeAsDouble, Time.frameCount); }
        if (LastGaussianRadius <= .001f) { return; }
        pass.Setup(material, LastGaussianRadius, LastMotionPixels, camera.pixelWidth, camera.pixelHeight);
        renderer.EnqueuePass(pass);
    }

    public static float GaussianRadius(float fatigue, float maximum = 2f)
    {
        if (float.IsNaN(fatigue) || float.IsInfinity(fatigue)) { return 0; }
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(25, 100, fatigue)) * Mathf.Clamp(maximum, 0, 2);
    }

    /// <summary>Rotational shutter blur suits the seated camera; no motion-vector buffer or history texture.</summary>
    public static Vector2 MotionPixels(Quaternion previous, Quaternion current, float fov, float aspect,
        int height, double elapsed, float fatigue, float maximum = 4f)
    {
        if (fatigue <= 50 || float.IsNaN(fatigue) || float.IsInfinity(fatigue) || elapsed < .001 || elapsed > .2 || height <= 0 || aspect <= 0)
        { return Vector2.zero; }
        if (Quaternion.Angle(previous, current) > 12f) { return Vector2.zero; }
        var direction = Quaternion.Inverse(current) * previous * Vector3.forward;
        float projection = height / (2f * Mathf.Tan(Mathf.Clamp(fov, 1, 179) * .5f * Mathf.Deg2Rad));
        var pixels = new Vector2(direction.x, direction.y) * (projection / Mathf.Max(.1f, direction.z));
        pixels *= .025f / (float)elapsed;
        float strength = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(50, 100, fatigue));
        return Vector2.ClampMagnitude(pixels * strength, Mathf.Clamp(maximum, 0, 4) * strength);
    }

    protected override void Dispose(bool disposing)
    { CoreUtils.Destroy(material); material = null; history.Reset(); }

    public sealed class MotionHistory
    {
        private bool valid;
        private int frame, width, height;
        private double time;
        private float fov;
        private Vector3 position;
        private Quaternion rotation;
        private Vector2 cached;
        public void Reset() { valid = false; cached = Vector2.zero; }
        public Vector2 Sample(Camera camera, float fatigue, float maximum, double now, int currentFrame)
        {
            if (valid && frame == currentFrame) { return cached; }
            var transform = camera.transform;
            bool continuous = valid && camera.pixelWidth == width && camera.pixelHeight == height &&
                Mathf.Abs(camera.fieldOfView - fov) < .5f && (transform.position - position).sqrMagnitude < .0225f;
            cached = continuous ? MotionPixels(rotation, transform.rotation, camera.fieldOfView, camera.aspect,
                camera.pixelHeight, now - time, fatigue, maximum) : Vector2.zero;
            valid = true; frame = currentFrame; time = now; fov = camera.fieldOfView;
            width = camera.pixelWidth; height = camera.pixelHeight; position = transform.position; rotation = transform.rotation;
            return cached;
        }
    }

    private sealed class OpticsPass : ScriptableRenderPass
    {
        private static readonly int OpticsId = Shader.PropertyToID("_FatigueOptics");
        private readonly FatigueBlurFeature owner;
        private readonly MaterialPropertyBlock properties = new();
        private Material material;
        private bool moving;
        public OpticsPass(FatigueBlurFeature owner) { this.owner = owner; requiresIntermediateTexture = true; }
        public void Setup(Material value, float radius, Vector2 motion, int width, int height)
        {
            material = value; moving = motion.sqrMagnitude > .0025f;
            properties.SetVector(OpticsId, new Vector4(radius / Mathf.Max(1, width), radius / Mathf.Max(1, height),
                motion.x / Mathf.Max(1, width), motion.y / Mathf.Max(1, height)));
        }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) { return; }
            var source = resources.activeColorTexture;
            var descriptor = graph.GetTextureDesc(source);
            descriptor.name = "Fatigue Gaussian 2px"; descriptor.clearBuffer = false; descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            var blurred = graph.CreateTexture(descriptor);
            var gaussian = new RenderGraphUtils.BlitMaterialParameters(source, blurred, material, 0) { propertyBlock = properties };
            graph.AddBlitPass(gaussian, passName: "Fatigue / Gaussian (max 2px)");
            if (moving)
            {
                descriptor.name = "Fatigue Camera Motion";
                var motion = graph.CreateTexture(descriptor);
                var shutter = new RenderGraphUtils.BlitMaterialParameters(blurred, motion, material, 1) { propertyBlock = properties };
                graph.AddBlitPass(shutter, passName: "Fatigue / Camera motion (max 4px)");
                resources.cameraColor = motion;
            }
            else { resources.cameraColor = blurred; }
            owner.RecordedPasses++;
        }
    }
}
```

## Assets/Editor/FatigueBlurValidation.cs

```csharp
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Runs serially inside the bounded preview process; never writes scene/save/settings.</summary>
public static class FatigueBlurValidation
{
    [MenuItem("DonDon/Settings/Install fatigue optics")]
    public static void Install()
    {
        foreach (string path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            var feature = data.rendererFeatures.OfType<FatigueBlurFeature>().SingleOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FatigueBlurFeature>();
                feature.name = "Fatigue Optics"; feature.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(feature, data); data.rendererFeatures.Add(feature);
            }
            var serialized = new SerializedObject(feature);
            serialized.FindProperty("shader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/UI/SystemMenu/FatigueBlur.shader");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var renderer = new SerializedObject(data);
            var map = renderer.FindProperty("m_RendererFeatureMap"); map.arraySize = data.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string guid, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            renderer.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true); EditorUtility.SetDirty(feature); EditorUtility.SetDirty(data); data.SetDirty();
        }
        AssetDatabase.SaveAssets();
    }

    private static void Check(bool pass, string description)
    {
        string output = Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        File.AppendAllText(Path.Combine(output, "blur-checks.txt"), (pass ? "PASS " : "FAIL ") + description + "\n");
        if (!pass) { throw new InvalidOperationException(description); }
    }
    public static FatigueBlurFeature CurrentFeature()
    {
        var pipeline = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        if (pipeline == null) { pipeline = (UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline; }
        var serialized = new SerializedObject(pipeline);
        var data = (UniversalRendererData)serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
        return data.rendererFeatures.OfType<FatigueBlurFeature>().Single();
    }
    public static void Verify(Camera camera)
    {
        Check(PlayerRestController.Instance.ViewCamera == camera, "Optics attached only to actual player camera");
        for (int fatigue = 0; fatigue <= 160; fatigue++)
        {
            float radius = FatigueBlurFeature.GaussianRadius(fatigue, 99);
            Check(radius >= 0 && radius <= 2, "Gaussian output-pixel cap at fatigue " + fatigue);
        }
        Check(FatigueBlurFeature.GaussianRadius(25) == 0 && FatigueBlurFeature.GaussianRadius(100) == 2, "Clear until 25%, reaches 2px at 100%");
        Check(FatigueBlurFeature.GaussianRadius(float.NaN) == 0, "Invalid fatigue cannot contaminate pixels");
        var start = Quaternion.identity; var turned = Quaternion.Euler(0, .2f, 0);
        Vector2 Motion(float fatigue, double dt, int height = 1080) => FatigueBlurFeature.MotionPixels(start, turned, 45, 16f / 9, height, dt, fatigue, 99);
        Check(Motion(50, .05).sqrMagnitude == 0, "No motion blur at fatigue <=50");
        Check(Motion(100, .05).magnitude > 0 && Motion(100, .05).magnitude <= 4, "Moving tired camera has capped shutter blur");
        Check(Motion(100, 1).sqrMagnitude == 0, "Stall/resume resets motion");
        Check(FatigueBlurFeature.MotionPixels(start, start, 45, 1.777f, 1080, .05, 100).sqrMagnitude == 0, "Stationary camera has no motion blur");
        Check(FatigueBlurFeature.MotionPixels(start, Quaternion.Euler(0, 45, 0), 45, 1.777f, 1080, .05, 100).sqrMagnitude == 0, "Camera cut resets motion");
        foreach (int height in new[] { 720, 900, 1080, 2160 })
        { Check(Motion(100, .016, height).magnitude <= 4, "Motion cap at output height " + height); }
        var oldTarget = camera.targetTexture; float oldFov = camera.fieldOfView; var oldPose = camera.transform.rotation;
        var oldPosition = camera.transform.position; double oldFatigue = PlayerRestController.Instance.State.fatigue;
        int quality = QualitySettings.GetQualityLevel();
        try
        {
            var history = new FatigueBlurFeature.MotionHistory();
            Check(history.Sample(camera, 100, 4, 1, 1) == Vector2.zero, "First motion frame starts clean");
            camera.transform.rotation = oldPose * Quaternion.Euler(0, .2f, 0);
            Check(history.Sample(camera, 100, 4, 1.05, 2).sqrMagnitude > 0, "History samples actual camera rotation");
            Check(history.Sample(camera, 100, 4, 1.10, 3) == Vector2.zero, "Motion stops on next stationary frame");
            camera.fieldOfView += 2;
            Check(history.Sample(camera, 100, 4, 1.15, 4) == Vector2.zero, "Zoom change discards history");
            camera.transform.position += Vector3.right;
            Check(history.Sample(camera, 100, 4, 1.20, 5) == Vector2.zero, "Position jump discards history");
            history.Reset(); Check(history.Sample(camera, 100, 4, 1.25, 6) == Vector2.zero, "Pause reset discards history");
            camera.transform.SetPositionAndRotation(oldPosition, oldPose); camera.fieldOfView = oldFov;
            foreach (int preset in new[] { 0, 1 })
            {
                QualitySettings.SetQualityLevel(preset, true);
                foreach (int height in new[] { 720, 1080 }) { VerifyRender(camera, height * 16 / 9, height, preset); }
            }
        }
        finally
        {
            QualitySettings.SetQualityLevel(quality, true);
            camera.targetTexture = oldTarget; camera.fieldOfView = oldFov;
            camera.transform.SetPositionAndRotation(oldPosition, oldPose); PlayerRestController.Instance.State.fatigue = oldFatigue;
        }
    }
    private static void VerifyRender(Camera camera, int width, int height, int preset)
    {
        var target = new RenderTexture(width, height, 24); target.Create();
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        var active = RenderTexture.active; var previous = camera.targetTexture;
        string key = "quality" + preset + "-" + height;
        FatigueBlurFeature feature = null;
        try
        {
            camera.targetTexture = target; PlayerRestController.Instance.State.fatigue = 100;
            camera.Render(); feature = CurrentFeature();
            int rendered = feature.RecordedPasses;
            Check(rendered > 0 && feature.LastGaussianRadius == 2, key + " actual RenderGraph executed");
            feature.SetActive(false); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(false);
            var clear = pixels.GetPixels32();
            string output = Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
            File.WriteAllBytes(Path.Combine(output, key + "-clear.png"), pixels.EncodeToPNG());
            feature.SetActive(true); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(false);
            var blurred = pixels.GetPixels32(); long difference = 0; double sharpBefore = 0, sharpAfter = 0;
            for (int y = height / 4; y < height * 3 / 4; y++)
            {
                for (int x = width / 4; x < width * 3 / 4; x++)
                {
                    int p = y * width + x;
                    difference += Math.Abs(clear[p].r - blurred[p].r) + Math.Abs(clear[p].g - blurred[p].g) + Math.Abs(clear[p].b - blurred[p].b);
                    sharpBefore += Math.Abs(clear[p].r - clear[p + 1].r) + Math.Abs(clear[p].g - clear[p + 1].g);
                    sharpAfter += Math.Abs(blurred[p].r - blurred[p + 1].r) + Math.Abs(blurred[p].g - blurred[p + 1].g);
                }
            }
            File.WriteAllBytes(Path.Combine(output, key + "-blur.png"), pixels.EncodeToPNG());
            Check(difference > 0 && sharpAfter < sharpBefore, key + " image softened; gradient ratio=" + (sharpAfter / sharpBefore).ToString("F4"));
            PlayerRestController.Instance.State.fatigue = 0; rendered = feature.RecordedPasses; camera.Render();
            Check(feature.RecordedPasses == rendered && feature.LastGaussianRadius == 0, key + " rested camera skips GPU pass");
            PlayerRestController.Instance.State.fatigue = 100;
            PauseMenuController.Instance.Open(); rendered = feature.RecordedPasses; camera.Render();
            Check(feature.RecordedPasses == rendered && feature.LastMotionPixels == Vector2.zero, key + " ESC/settings excludes fatigue optics");
            PauseMenuController.Instance.Resume();
        }
        finally
        {
            if (feature != null) { feature.SetActive(true); }
            camera.targetTexture = previous; RenderTexture.active = active; target.Release(); Object.Destroy(target); Object.Destroy(pixels);
        }
    }
}
```

## Assets/Resources/UI/SystemMenu/FatigueBlur.shader

```hlsl
Shader "Hidden/DonDon/FatigueBlur"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _FatigueOptics;
        half4 ReadScene(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
        }
        half4 Gaussian(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            // Bilinear pairs implement the 5x5 Gaussian [1 4 6 4 1] with nine reads.
            // At radius 2, samples at +/-1.2 combine the adjacent +/-1 and +/-2 texels.
            // Merely spacing the old 3x3 taps two pixels apart would leave visible gaps.
            float2 x = float2(_FatigueOptics.x * .6, 0), y = float2(0, _FatigueOptics.y * .6);
            half4 value = ReadScene(uv) * .140625;
            value += (ReadScene(uv-x) + ReadScene(uv+x) + ReadScene(uv-y) + ReadScene(uv+y)) * .1171875;
            value += (ReadScene(uv-x-y) + ReadScene(uv+x-y) + ReadScene(uv-x+y) + ReadScene(uv+x+y)) * .09765625;
            return value;
        }
        half4 CameraMotion(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord, direction = _FatigueOptics.zw;
            // Symmetric shutter, no lingering ghost frame; full end-to-end span <= four output pixels.
            return (ReadScene(uv-direction*.5) + ReadScene(uv-direction*.25) + ReadScene(uv)
                + ReadScene(uv+direction*.25) + ReadScene(uv+direction*.5)) * .2;
        }
        ENDHLSL
        Pass
        {
            Name "Gaussian"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Gaussian
            ENDHLSL
        }
        Pass
        {
            Name "Camera Motion"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CameraMotion
            ENDHLSL
        }
    }
}
```

## FatigueVideoCapture.cs · Run

### 원본
```csharp
    public static void Run()
    {
        output = Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if (string.IsNullOrEmpty(output) || !Path.GetFullPath(output).StartsWith(Path.GetFullPath(".")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
        { throw new InvalidOperationException("Use the bounded Settings runner with an output folder in this workspace."); }
        Directory.CreateDirectory(Path.Combine(output,"frames"));
        hadLanguage = PlayerPrefs.HasKey("DonDon.Language"); priorLanguage = PlayerPrefs.GetString("DonDon.Language");
        File.WriteAllText(Path.Combine(output,"language-before.txt"),(hadLanguage ? "1\n" : "0\n")+priorLanguage);
        PlayerPrefs.SetString("DonDon.Language","ko"); PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled; oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup; EditorApplication.update += Monitor; Application.logMessageReceived += Log;
        EditorApplication.EnterPlaymode();
    }
```

### 수정본
```csharp
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
```

## FatigueVideoCapture.cs · Prepare

### 원본
```csharp
    private static void Prepare()
    {
        camera = Camera.main; position = camera.transform.position; rotation = camera.transform.rotation;
        Time.timeScale = 0;
        var rest = PlayerRestController.Instance; rest.enabled = false;
        effect = rest.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "Image :: Tired Eyes");
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
        File.WriteAllText(Path.Combine(output,"capture-info.txt"),"1280x720 / 20 fps / 36 seconds\nProduction FatigueVision shader on actual TradeScene camera.\nMarket frozen for comparison. Fatigue progression is preview-only. No gameplay tuning changed.\n0-4s clear; 4-10s 0->60; 10-16s 60->100; 16-31s 100; 31-36s recovery comparison.\n");
    }
```

### 수정본
```csharp
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
```

## FatigueVideoCapture.cs · RenderFrame

### 원본
```csharp
    private static void RenderFrame()
    {
        float time = frame/(float)Rate, fatigue = Fatigue(time);
        camera.transform.SetPositionAndRotation(position,rotation); Time.timeScale = 0;
        effect.enabled = fatigue > 25; effect.material.SetFloat("_Fatigue",fatigue/100);
        effect.material.SetFloat("_Breath",.5f+.5f*Mathf.Sin(time*.5f));
        caption.text = time < 4 ? "정상 시야" : time < 10 ? "피로 누적 · 시야 가장자리 어두워짐" : time < 16 ?
            "피로 심화 · 위아래 시야가 서서히 좁아짐" : time < 31 ? "피로 100% · 느리고 부드러운 눈꺼풀 움직임" : "회복 후 시야 비교";
        number.text = "피로 "+Mathf.RoundToInt(fatigue)+"%";
        meter.rectTransform.sizeDelta = new Vector2(1232*fatigue/100,2);
        Canvas.ForceUpdateCanvases();
        var old = RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,Width,Height),0,0); pixels.Apply(false);
            File.WriteAllBytes(Path.Combine(output,"frames",frame.ToString("D4")+".jpg"),pixels.EncodeToJPG(92));
            if (frame % 100 == 0) { File.WriteAllText(Path.Combine(output,"progress.txt"),frame+" / "+Frames); }
        }
        finally { RenderTexture.active = old; }
    }
```

### 수정본
```csharp
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
```

## PlayerRestController.cs · Awake

### 원본
```csharp
    private void Awake()
    {
        Instance = this; day = GetComponent<MarketDayController>();
        var root = new GameObject("Area :: Fatigue Vision", typeof(RectTransform), typeof(Canvas)); root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 29900;
        var graphic = new GameObject("Image :: Tired Eyes", typeof(RectTransform), typeof(RawImage)); graphic.transform.SetParent(root.transform, false);
        overlay = graphic.GetComponent<RawImage>(); overlay.raycastTarget = false;
        var rect = overlay.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var shader = Resources.Load<Shader>("UI/SystemMenu/FatigueVision");
        if (shader != null) { vision = new Material(shader); overlay.material = vision; }
        overlay.enabled = false;
    }
```

### 수정본
```csharp
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
```

## 2픽셀 후속 조정 · FatigueBlurFeature.cs

Inspector 범위와 초기값: `[Range(0, 1)] ... = 1f` → `[Range(0, 2)] ... = 2f`. PC/Mobile 렌더러의 저장된 값도 1→2로 변경했습니다. 모션 블러 설정은 유지했습니다.

### GaussianRadius 원본 (1픽셀)
```csharp
    public static float GaussianRadius(float fatigue, float maximum = 1f)
    {
        if (float.IsNaN(fatigue) || float.IsInfinity(fatigue)) { return 0; }
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(25, 100, fatigue)) * Mathf.Clamp(maximum, 0, 1);
    }
```

### GaussianRadius 수정본 (2픽셀)
```csharp
    public static float GaussianRadius(float fatigue, float maximum = 2f)
    {
        if (float.IsNaN(fatigue) || float.IsInfinity(fatigue)) { return 0; }
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(25, 100, fatigue)) * Mathf.Clamp(maximum, 0, 2);
    }
```

### RecordRenderGraph 원본 (1픽셀)
```csharp
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) { return; }
            var source = resources.activeColorTexture;
            var descriptor = graph.GetTextureDesc(source);
            descriptor.name = "Fatigue Gaussian 1px"; descriptor.clearBuffer = false; descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            var blurred = graph.CreateTexture(descriptor);
            var gaussian = new RenderGraphUtils.BlitMaterialParameters(source, blurred, material, 0) { propertyBlock = properties };
            graph.AddBlitPass(gaussian, passName: "Fatigue / Gaussian (max 1px)");
            if (moving)
            {
                descriptor.name = "Fatigue Camera Motion";
                var motion = graph.CreateTexture(descriptor);
                var shutter = new RenderGraphUtils.BlitMaterialParameters(blurred, motion, material, 1) { propertyBlock = properties };
                graph.AddBlitPass(shutter, passName: "Fatigue / Camera motion (max 4px)");
                resources.cameraColor = motion;
            }
            else { resources.cameraColor = blurred; }
            owner.RecordedPasses++;
        }
```

### RecordRenderGraph 수정본 (2픽셀)
```csharp
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) { return; }
            var source = resources.activeColorTexture;
            var descriptor = graph.GetTextureDesc(source);
            descriptor.name = "Fatigue Gaussian 2px"; descriptor.clearBuffer = false; descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            var blurred = graph.CreateTexture(descriptor);
            var gaussian = new RenderGraphUtils.BlitMaterialParameters(source, blurred, material, 0) { propertyBlock = properties };
            graph.AddBlitPass(gaussian, passName: "Fatigue / Gaussian (max 2px)");
            if (moving)
            {
                descriptor.name = "Fatigue Camera Motion";
                var motion = graph.CreateTexture(descriptor);
                var shutter = new RenderGraphUtils.BlitMaterialParameters(blurred, motion, material, 1) { propertyBlock = properties };
                graph.AddBlitPass(shutter, passName: "Fatigue / Camera motion (max 4px)");
                resources.cameraColor = motion;
            }
            else { resources.cameraColor = blurred; }
            owner.RecordedPasses++;
        }
```
