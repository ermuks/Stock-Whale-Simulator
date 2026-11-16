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
