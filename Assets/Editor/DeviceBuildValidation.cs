using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class DeviceBuildValidation
{
    public static void Build()
    {
        const string folder = "Validation/DeviceCanvasUI/";
        try
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes 1/TradeScene.unity" },
                locationPathName = folder + "Player/DonDon.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            var result = report.summary;
            File.WriteAllText(folder + "build-results.txt", result.result + "\nErrors: " + result.totalErrors + "\nWarnings: " + result.totalWarnings + "\nBytes: " + result.totalSize + "\nSeconds: " + result.totalTime.TotalSeconds + "\nOutput: " + result.outputPath);
            EditorApplication.Exit(result.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception error)
        {
            File.WriteAllText(folder + "build-results.txt", error.ToString());
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
