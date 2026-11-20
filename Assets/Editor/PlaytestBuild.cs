using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 플레이용 빌드. 명령줄에서 부른다:
//   Unity.exe -batchmode -quit -projectPath . -executeMethod PlaytestBuild.Build -logFile Logs/playtest-build.log
// 번역 표(Addressables)를 먼저 굽고, 빌드 설정에 켜져 있는 장면으로 Playtest 폴더에 출시용(개발 빌드 아님) 실행 파일을 만든다.
// 번역 표를 CSV에서 다시 들여오지는 않는다. 지금 표 그대로 굽는다.
public static class PlaytestBuild
{
    private const string Folder = "Playtest";
    public static void Build()
    {
        try
        {
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error)) { throw new InvalidOperationException("Addressables: " + content.Error); }
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes) { if (scene.enabled) { scenes.Add(scene.path); } }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = Folder + "/" + PlayerSettings.productName + ".exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            var result = report.summary;
            File.WriteAllText(Folder + "/build-results.txt", result.result + "\nErrors: " + result.totalErrors + "\nWarnings: " + result.totalWarnings + "\nBytes: " + result.totalSize + "\nSeconds: " + result.totalTime.TotalSeconds.ToString("0") + "\n");
            EditorApplication.Exit(result.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/build-results.txt", "FAILED\n" + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
