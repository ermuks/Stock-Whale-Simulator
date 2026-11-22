using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>1번 태블릿 해금 화면을 실제 씬 카메라로 찍어 확인한다. 시장에는 개입하지 않는다.</summary>
public static class UnlockScreenValidation
{
    private static string output;
    private static int phase, errors;
    private static double next, began;
    private static bool oldOptions, done;
    private static EnterPlayModeOptions oldFlags;
    private static StreamWriter log;

    public static void Run()
    {
        output = Environment.GetEnvironmentVariable("DONDON_SETTINGS_OUTPUT");
        if (string.IsNullOrEmpty(output))
        { throw new InvalidOperationException("Output folder required."); }

        Directory.CreateDirectory(output);
        log = new StreamWriter(Path.Combine(output, "checks.txt"), false, new System.Text.UTF8Encoding(false));
        EditorSceneManager.OpenScene("Assets/Scenes 1/TradeScene.unity");
        oldOptions = EditorSettings.enterPlayModeOptionsEnabled;
        oldFlags = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        began = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += Log;
        EditorApplication.update += Step;
        EditorApplication.EnterPlaymode();
    }

    private static void Log(string text, string stack, LogType kind)
    {
        if (kind != LogType.Error && kind != LogType.Exception)
        { return; }

        errors++;
        log?.WriteLine("ERROR " + text + "\n" + stack);
        log?.Flush();
    }

    private static void Check(bool success, string message)
    {
        log.WriteLine((success ? "PASS " : "FAIL ") + message);
        log.Flush();
        if (!success)
        {
            errors++;
        }
    }

    private static void Step()
    {
        if (done)
        { return; }

        if (EditorApplication.timeSinceStartup - began > 150)
        {
            Check(false, "time budget");
            Finish();
            return;
        }

        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next)
        { return; }

        try
        {
            var manager = StockManager.Instance;
            var day = MarketDayController.Instance;
            if (manager == null || day == null)
            { return; }

            if (phase == 0)
            {
                Application.targetFrameRate = 20;
                QualitySettings.vSyncCount = 0;
                if (NewGame.CanBegin) { NewGame.Begin(); }

                if (manager.SetupConfirmed && day.Ready)
                {
                    phase = 1;
                    next = EditorApplication.timeSinceStartup + 3;
                }

                return;
            }

            var tablet = UnityEngine.Object.FindObjectsByType<TabletAccountUI>(FindObjectsInactive.Include).Single(t => !t.second);
            var progression = PlayerProgression.Instance;
            if (phase == 1)
            {
                DeskViewController.Instance.Focus(DeskViewTarget.TabletOne);
                tablet.ShowPage(TabletAccountUI.Page.Unlocks);
                phase = 2;
                next = EditorApplication.timeSinceStartup + 4;
                return;
            }

            var page = UnityEngine.Object.FindAnyObjectByType<UnlockGridUI>(FindObjectsInactive.Include);
            if (phase == 2)
            {
                Check(page != null && page.gameObject.activeInHierarchy, "unlock page is active on tablet 1");
                var buttons = page == null ? new Button[0] : page.GetComponentsInChildren<Button>(true);
                var texts = page == null ? new TMPro.TMP_Text[0] : page.GetComponentsInChildren<TMPro.TMP_Text>(false);
                log.WriteLine("buttons: " + string.Join(" | ", buttons.Select(b => b.name + (b.interactable ? "" : " (off)"))));
                log.WriteLine("visible texts: " + string.Join(" | ", texts.Select(t => t.text.Replace("\n", " "))));
                int overflow = texts.Count(t => t.isTextOverflowing);
                log.WriteLine("texts overflowing their rect: " + overflow + " of " + texts.Length);
                Check(texts.Any(t => t.text.Contains("5,000,000")), "selected item shows its cost");
                Capture("unlock-available", 1920, 1080);
                Capture("unlock-available", 1600, 900);
                decimal cash = manager.playerAccount.cash;
                var start = buttons.FirstOrDefault(b => b.interactable && b.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text.Contains("해금하기")));
                Check(start != null, "an enabled 해금하기 button exists");
                if (start != null)
                {
                    start.onClick.Invoke();
                }

                Check(progression.PendingUnlock == PlayerProgression.CrowdValueUnlock && manager.playerAccount.cash == cash - 5000000m, "button click starts the unlock and charges 5,000,000 once");
                phase = 3;
                next = EditorApplication.timeSinceStartup + 3;
                return;
            }

            if (phase == 3)
            {
                var texts = page.GetComponentsInChildren<TMPro.TMP_Text>(false);
                log.WriteLine("in-progress texts: " + string.Join(" | ", texts.Select(t => t.text.Replace("\n", " "))));
                Capture("unlock-in-progress", 1920, 1080);
                Finish();
            }
        }
        catch (Exception exception)
        {
            errors++;
            log.WriteLine("EXCEPTION " + exception);
            Finish();
        }
    }

    private static void Capture(string name, int width, int height)
    {
        var camera = Camera.main;
        var prior = camera.targetTexture;
        var active = RenderTexture.active;
        var target = new RenderTexture(width, height, 24);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(output, name + "-" + width + "x" + height + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = prior;
            RenderTexture.active = active;
            target.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void Finish()
    {
        done = true;
        log.WriteLine("errors=" + errors);
        log.Dispose();
        EditorSettings.enterPlayModeOptionsEnabled = oldOptions;
        EditorSettings.enterPlayModeOptions = oldFlags;
        EditorApplication.update -= Step;
        Application.logMessageReceived -= Log;
        EditorApplication.Exit(errors == 0 ? 0 : 1);
    }
}
